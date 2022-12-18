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

namespace NaiveMq.Service
{
    public sealed class NaiveMqService : BackgroundService
    {
        public bool Loaded => _loaded;

        public bool Online => _online;

        public Storage Storage { get; private set; }

        private CancellationToken _stoppingToken;
        
        private bool _loaded;

        private bool _online;

        private TcpListener _listener;

        private readonly ILogger<NaiveMqService> _logger;
        
        private readonly ILogger<NaiveMqClient> _clientLogger;
        
        private readonly NaiveMqServiceOptions _options;

        private readonly IPersistentStorage _persistentStorage;

        private readonly Dictionary<Type, Type> _commandHandlers = new();

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
            _options = options.Value;
            _persistentStorage = persistentStorage;

            InitCommands();
        }

        private void InitCommands()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                var ihandler = type.GetInterfaces().FirstOrDefault(y => y.IsGenericType && typeof(IHandler<IRequest<IResponse>, IResponse>).Name == y.GetGenericTypeDefinition().Name);
                if (ihandler != null)
                {
                    _commandHandlers.Add(ihandler.GenericTypeArguments.First(), type);
                    continue;
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            Storage = new Storage(_options, _persistentStorage, _logger, _clientLogger, _stoppingToken);
            await new PersistentStorageLoader(Storage, _logger, _stoppingToken).LoadAsync();

            _loaded = true;

            _listener = new TcpListener(IPAddress.Any, _options.Port);
            while (!_stoppingToken.IsCancellationRequested)
            {
                if (_listener.Server.IsBound)
                {
                    AddClient(await _listener.AcceptTcpClientAsync(_stoppingToken));
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
            Storage.Dispose();
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

                    _online = true;
                    _logger.LogInformation($"Server listenter started on port {_options.Port}.");
                    break;
                }
                catch (Exception ex)
                {
                    if (lastError != ex.GetBaseException().Message)
                    {
                        _logger.LogError(ex, $"Cannot start server listener on port {_options.Port}. Retry in {_options.ListenerRecoveryInterval}.");
                        lastError = ex.GetBaseException().Message;
                    }
                }

                await Task.Delay(_options.ListenerRecoveryInterval, _stoppingToken);
            }
        }

        private void Offline()
        {
            _listener.Stop();
            Storage.Cluster.Stop();

            _online = false;
            _logger.LogWarning($"Server listenter stopped on port {_options.Port}.");
        }

        private void AddClient(TcpClient tcpClient)
        {
            NaiveMqClient client = null;

            try
            {
                client = new NaiveMqClient(new NaiveMqClientOptions { TcpClient = tcpClient }, _clientLogger, _stoppingToken);

                client.OnStop += OnClientStop;
                client.OnReceiveErrorAsync += OnClientReceiveErrorAsync;
                client.OnReceiveRequestAsync += OnClientReceiveRequestAsync;
                client.OnReceiveCommandAsync += OnClientReceiveCommandAsync;
                client.OnSendCommandAsync += OnClientSendCommandAsync;
                client.OnSendMessageAsync += OnClientSendMessageAsync;
                client.OnReceiveMessageAsync += OnClientReceiveMessageAsync;
                client.Start();
                
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

        private void OnClientStop(NaiveMqClient sender)
        {
            Storage.TryRemoveClient(sender);
        }

        private Task OnClientSendCommandAsync(NaiveMqClient sender, ICommand command)
        {
            Storage.WriteCounter.Add();
            return Task.CompletedTask;
        }

        private Task OnClientSendMessageAsync(NaiveMqClient sender, Client.Commands.Message command)
        {
            Storage.WriteMessageCounter.Add();
            return Task.CompletedTask;
        }

        private Task OnClientReceiveCommandAsync(NaiveMqClient sender, ICommand command)
        {
            Storage.ReadCounter.Add();
            return Task.CompletedTask;
        }

        private Task OnClientReceiveMessageAsync(NaiveMqClient sender, Client.Commands.Message command)
        {
            Storage.ReadMessageCounter.Add();
            return Task.CompletedTask;
        }

        private Task OnClientReceiveErrorAsync(NaiveMqClient sender, Exception ex)
        {
            _logger.LogError(ex, "Client receive error.");
            return Task.CompletedTask;
        }

        private async Task OnClientReceiveRequestAsync(NaiveMqClient sender, IRequest request)
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

                await SendErrorAsync(sender, request, ErrorCode.UnexpectedCommandHandlerExecutionError.ToString(), ex.GetBaseException().Message);
            }
        }

        private async Task SendErrorAsync(NaiveMqClient sender, IRequest request, string errorCode, string errorMessage)
        {
            await SendAsync(sender, Confirmation.Error(request, errorCode, errorMessage));
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

        private async Task<IResponse> HandleRequestAsync(NaiveMqClient sender, IRequest command)
        {
            if (_commandHandlers.TryGetValue(command.GetType(), out var commandHandler))
            {
                var method = commandHandler.GetMethod(nameof(IHandler<IRequest<IResponse>, IResponse>.ExecuteAsync));
                IDisposable instance = null;

                try
                {
                    Storage.TryGetClient(sender.Id, out var clientContext);

                    instance = (IDisposable)Activator.CreateInstance(commandHandler);
                    var task = (Task)method.Invoke(instance, new object[] { clientContext, command });
                    await task;

                    var resultProperty = task.GetType().GetProperty("Result");
                    var result = (IResponse)resultProperty.GetValue(task);

                    return result;
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException;
                }
                catch
                {
                    throw;
                }
                finally
                {
                    if (instance != null)
                    {
                        instance.Dispose();
                    }
                }
            }
            else
            {
                throw new ServerException(ErrorCode.CommandHandlerNotFound,
                    string.Format(ErrorCode.CommandHandlerNotFound.GetDescription(), command.GetType().Name));
            }
        }
    }
}