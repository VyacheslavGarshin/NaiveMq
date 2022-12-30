using System.Net.Sockets;
using System.Net;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NaiveMq.Client.Common;
using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Handlers;
using NaiveMq.Service.PersistentStorage;
using NaiveMq.Client;
using NaiveMq.Service.Counters;

namespace NaiveMq.Service
{
    public sealed class NaiveMqService : BackgroundService
    {
        public static Dictionary<Type, Type> CommandHandlers { get; } = new();

        public bool Loaded { get; private set; }

        public bool Online { get; private set; }

        public NaiveMqServiceOptions Options { get; }

        public Storage Storage { get; private set; }

        public SpeedCounterService SpeedCounterService { get; } = new();

        public ServiceCounters Counters { get; }

        public ILogger<NaiveMqClient> ClientLogger { get; set; }
        
        private CancellationToken _stoppingToken;
        
        private TcpListener _listener;

        private readonly ILogger<NaiveMqService> _logger;
                
        private readonly IPersistentStorage _persistentStorage;

        static NaiveMqService()
        {
            NaiveMqClient.RegisterCommands(typeof(NaiveMqService).Assembly);
        }

        public NaiveMqService(
            ILoggerFactory loggerFactory,
            IOptions<NaiveMqServiceOptions> options,
            IPersistentStorage persistentStorage)
        {
            _logger = loggerFactory.CreateLogger<NaiveMqService>();
            _persistentStorage = persistentStorage;

            ClientLogger = loggerFactory.CreateLogger<NaiveMqClient>();
            Options = options.Value;
            Counters = new(SpeedCounterService);

            RegisterHandlers(Assembly.GetExecutingAssembly());
        }

        public static void RegisterHandlers(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                var ihandler = type.GetInterfaces().FirstOrDefault(y => y.IsGenericType && typeof(IHandler<IRequest<IResponse>, IResponse>).Name == y.GetGenericTypeDefinition().Name);
                if (ihandler != null)
                {
                    CommandHandlers.Add(ihandler.GenericTypeArguments.First(), type);
                    continue;
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            Storage = new Storage(this, _persistentStorage, _logger, ClientLogger, _stoppingToken);
            await new PersistentStorageLoader(Storage, _logger, _stoppingToken).LoadAsync();

            Loaded = true;

            _listener = new TcpListener(IPAddress.Any, Options.Port);
            while (!_stoppingToken.IsCancellationRequested)
            {
                if (_listener.Server.IsBound)
                {
                    AddTcpClient(await _listener.AcceptTcpClientAsync(_stoppingToken));
                }
                else
                {
                    await OnlineAsync();
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            Offline();

            SpeedCounterService.Dispose();

            Storage.Dispose();

            Counters.Dispose();
        }

        public async Task<IResponse> ExecuteCommandAsync(IRequest command, ClientContext clientContext)
        {
            if (CommandHandlers.TryGetValue(command.GetType(), out var commandHandlerType))
            {
                return await ((IHandler)Activator.CreateInstance(commandHandlerType)).ExecuteAsync(clientContext, command, _stoppingToken);
            }
            else
            {
                throw new ServerException(ErrorCode.CommandHandlerNotFound, new[] { command.GetType().Name });
            }
        }

        private async Task OnlineAsync()
        {
            var lastError = string.Empty;

            while (true)
            {
                try
                {
                    _listener.Start();
                    Storage.Cluster.Start();

                    Online = true;
                    _logger.LogInformation("Server listenter started on port {Port}.", Options.Port);
                    break;
                }
                catch (Exception ex)
                {
                    if (lastError != ex.Message)
                    {
                        _logger.LogError(ex, "Cannot start server listener on port {Port}. Retry in {ListenerRecoveryInterval}.", Options.Port, Options.ListenerRecoveryInterval);
                        lastError = ex.Message;
                    }
                }

                await Task.Delay(Options.ListenerRecoveryInterval, _stoppingToken);
            }
        }

        private void Offline()
        {
            _listener.Stop();
            Storage.Cluster.Stop();

            Online = false;
            _logger.LogWarning("Server listenter stopped on port {Port}.", Options.Port);
        }

        private void AddTcpClient(TcpClient tcpClient)
        {
            NaiveMqClientWithContext client = null;

            try
            {
                var options = Options.ClientOptions.Copy();

                options.TcpClient = tcpClient;
                options.AutoStart = false;
                options.AutoRestart = false;

                var clientContext = new ClientContext
                {
                    Storage = Storage,
                    Logger = _logger
                };

                client = new NaiveMqClientWithContext(options, clientContext, ClientLogger, _stoppingToken);
                client.OnStop += Client_OnStop;
                client.OnReceiveErrorAsync += Client_OnReceiveErrorAsync;
                client.OnReceiveRequestAsync += Client_OnReceiveRequestAsync;
                client.OnReceiveCommandAsync += Client_OnReceiveCommandAsync;
                client.OnSendCommandAsync += Client_OnSendCommandAsync;
                client.Start(false);

                Storage.TryAddClient(client);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during adding client.");

                if (client != null)
                {
                    Storage.TryRemoveClient(client);
                }
            }
        }

        private void Client_OnStop(NaiveMqClient sender)
        {
            Storage.TryRemoveClient(sender as NaiveMqClientWithContext);
        }

        private Task Client_OnSendCommandAsync(NaiveMqClient sender, ICommand command)
        {
            Counters.WriteCommand.Add();
            return Task.CompletedTask;
        }

        private Task Client_OnReceiveCommandAsync(NaiveMqClient sender, ICommand command)
        {
            Counters.ReadCommand.Add();
            return Task.CompletedTask;
        }

        private Task Client_OnReceiveErrorAsync(NaiveMqClient sender, Exception ex)
        {
            _logger.LogError(ex, "Client receive error.");
            return Task.CompletedTask;
        }

        private async Task Client_OnReceiveRequestAsync(NaiveMqClient sender, IRequest request)
        {
            IResponse response;
            var senderWithContext = sender as NaiveMqClientWithContext;

            try
            {
                response = await HandleRequestAsync(senderWithContext, request);                
            }
            catch (ServerException ex)
            {
                response = Confirmation.Error(request, ex.ErrorCode.ToString(), ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception on handling request.");
                response = Confirmation.Error(request, ErrorCode.UnexpectedCommandHandlerExecutionError.ToString(), ex.Message);
            }

            if (response != null)
            {
                try
                {
                    await senderWithContext.SendAsync(response, _stoppingToken);
                }
                catch (ClientException)
                {
                    Storage.TryRemoveClient(senderWithContext);
                }
                catch (Exception ex)
                {
                    Storage.TryRemoveClient(senderWithContext);
                    _logger.LogError(ex, "Unexpected error on sending response.");
                }
            }
        }

        private async Task<IResponse> HandleRequestAsync(NaiveMqClientWithContext sender, IRequest request)
        {
            try
            {
                var result = await ExecuteCommandAsync(request, sender.Context);

                if (Storage.Cluster.Started && request is IReplicable)
                {
                    await Storage.Cluster.ReplicateRequestAsync(request, sender.Context.User.Entity.Username);
                }

                return result;
            }
            catch (Exception ex)
            {
                TrackHandlerError(request, sender.Context, ex);

                throw;
            }
        }

        private void TrackHandlerError(IRequest request, ClientContext clientContext, Exception ex)
        {
            if (clientContext.Tracking)
            {
                if (clientContext.TrackFailedRequests.Count < Options.TrackFailedRequestsLimit)
                {
                    clientContext.TrackFailedRequests.Add(request.Id);
                    clientContext.TrackLastErrorCode = (ex as ServerException)?.ErrorCode.ToString();
                    clientContext.TrackLastErrorMessage = ex.Message;
                }
                else
                {
                    clientContext.TrackOverflow = true;
                }
            }
        }
    }
}