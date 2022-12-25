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

        private CancellationToken _stoppingToken;
        
        private TcpListener _listener;

        private readonly ILogger<NaiveMqService> _logger;
        
        private readonly ILogger<NaiveMqClient> _clientLogger;
        
        private readonly IPersistentStorage _persistentStorage;

        private Timer timer;

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
            _clientLogger = loggerFactory.CreateLogger<NaiveMqClient>();
            _persistentStorage = persistentStorage;

            Options = options.Value;
            Counters = new(SpeedCounterService);

            RegisterHandlers(Assembly.GetExecutingAssembly());
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            Storage = new Storage(this, _persistentStorage, _logger, _clientLogger, _stoppingToken);
            await new PersistentStorageLoader(Storage, _logger, _stoppingToken).LoadAsync();

            Loaded = true;

            timer = new Timer((s) =>
            {
                _logger.LogInformation($"{DateTime.Now:O};Read message/s;{Counters.Read.Second.Value};" +
                    $"Write message/s;{Counters.Write.Second.Value};" +
                    $"Read/s;{Counters.ReadCommand.Second.Value};" +
                    $"Write/s;{Counters.WriteCommand.Second.Value};" +
                    $"QueuesLength;{Counters.Length.Value};" +
                    $"QueuesVolume;{Counters.Volume.Value};" +
                    $"Subscriptions;{Counters.Subscriptions.Value};");
            }, null, 0, 1000);

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
            if (CommandHandlers.TryGetValue(command.GetType(), out var commandHandler))
            {
                return await ((IHandler)Activator.CreateInstance(commandHandler)).ExecuteAsync(clientContext, command, _stoppingToken);
            }
            else
            {
                throw new ServerException(ErrorCode.CommandHandlerNotFound,
                    string.Format(ErrorCode.CommandHandlerNotFound.GetDescription(), command.GetType().Name));
            }
        }

        public void RegisterHandlers(Assembly assembly)
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
                    _logger.LogInformation($"Server listenter started on port {Options.Port}.");
                    break;
                }
                catch (Exception ex)
                {
                    if (lastError != ex.Message)
                    {
                        _logger.LogError(ex, $"Cannot start server listener on port {Options.Port}. Retry in {Options.ListenerRecoveryInterval}.");
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
            _logger.LogWarning($"Server listenter stopped on port {Options.Port}.");
        }

        private void AddTcpClient(TcpClient tcpClient)
        {
            NaiveMqClient client = null;

            try
            {
                var options = new NaiveMqClientOptions
                {
                    TcpClient = tcpClient,
                    AutoStart = false,
                    AutoRestart = false,
                };

                client = new NaiveMqClient(options, _clientLogger, _stoppingToken);
                client.OnStop += Client_OnStop;
                client.OnReceiveErrorAsync += Client_OnReceiveErrorAsync;
                client.OnReceiveRequestAsync += Client_OnReceiveRequestAsync;
                client.OnReceiveCommandAsync += Client_OnReceiveCommandAsync;
                client.OnSendCommandAsync += Client_OnSendCommandAsync;
                client.OnSendMessageAsync += Client_OnSendMessageAsync;
                client.OnReceiveMessageAsync += Client_OnReceiveMessageAsync;
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
            Storage.TryRemoveClient(sender);
        }

        private Task Client_OnSendCommandAsync(NaiveMqClient sender, ICommand command)
        {
            Counters.WriteCommand.Add();
            return Task.CompletedTask;
        }

        private Task Client_OnSendMessageAsync(NaiveMqClient sender, Message command)
        {
            return Task.CompletedTask;
        }

        private Task Client_OnReceiveCommandAsync(NaiveMqClient sender, ICommand command)
        {
            Counters.ReadCommand.Add();
            return Task.CompletedTask;
        }

        private Task Client_OnReceiveMessageAsync(NaiveMqClient sender, Message command)
        {
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

            try
            {
                response = await HandleRequestAsync(sender, request);

                if (response != null)
                {
                    await SendAsync(sender, response);
                }
            }
            catch (ClientException ex)
            {
                await SendErrorAsync(sender, request, ex.ErrorCode.ToString(), ex.Message);
            }
            catch (ServerException ex)
            {
                await SendErrorAsync(sender, request, ex.ErrorCode.ToString(), ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Client receive request error.");

                await SendErrorAsync(sender, request, ErrorCode.UnexpectedCommandHandlerExecutionError.ToString(), ex.Message);
            }
        }

        private async Task SendErrorAsync(NaiveMqClient sender, IRequest request, string errorCode, string errorMessage)
        {
            if (request.Confirm)
            {
                await SendAsync(sender, Confirmation.Error(request, errorCode, errorMessage));
            }
        }

        private async Task SendAsync(NaiveMqClient client, IResponse response)
        {
            try
            {
                await client.SendAsync(response, _stoppingToken);
            }
            catch (ClientException)
            {
                Storage.TryRemoveClient(client);
            }
            catch (Exception ex)
            {
                Storage.TryRemoveClient(client);
                _logger.LogError(ex, "Unexpected error on sending response.");
            }
        }

        private async Task<IResponse> HandleRequestAsync(NaiveMqClient sender, IRequest request)
        {
            if (Storage.TryGetClientContext(sender.Id, out var clientContext))
            {
                try
                {
                    var result = await ExecuteCommandAsync(request, clientContext);

                    if (Storage.Cluster.Started && request is IReplicable)
                    {
                        await Storage.Cluster.ReplicateRequestAsync(request, clientContext.User.Entity.Username);
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    TrackHandlerError(request, clientContext, ex);

                    throw;
                }
            }
            else
            {
                throw new ServerException(ErrorCode.ClientNotFound);
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