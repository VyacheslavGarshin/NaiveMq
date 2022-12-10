﻿using System.Net.Sockets;
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
        public SpeedCounter WriteCounter { get; set; } = new(10);

        public SpeedCounter ReadCounter { get; set; } = new(10);

        public SpeedCounter ReadMessageCounter { get; set; } = new(10);

        public SpeedCounter WriteMessageCounter { get; set; } = new(10);

        public TimeSpan StartListenerErrorRetryInterval = TimeSpan.FromSeconds(1);

        public bool Loaded => _loaded;

        public bool Started => _started;

        private CancellationToken _stoppingToken;
        
        private bool _loaded;

        private bool _started;

        private TcpListener _listener;

        private Storage _storage;

        private readonly ILogger<NaiveMqService> _logger;
        
        private readonly ILogger<NaiveMqClient> _clientLogger;
        
        private readonly IOptions<NaiveMqServiceOptions> _options;

        private readonly IPersistentStorage _persistentStorage;

        private readonly Dictionary<Type, Type> _commandHandlers = new();

        public NaiveMqService(
            ILogger<NaiveMqService> logger,
            ILogger<NaiveMqClient> clientLogger,
            IOptions<NaiveMqServiceOptions> options,
            IPersistentStorage persistentStorage)
        {
            _logger = logger;
            _clientLogger = clientLogger;
            _options = options;
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

            _storage = new Storage(_options.Value, _persistentStorage, _logger, _stoppingToken);

            await new PersistentStorageLoader(_storage, _logger, _stoppingToken).LoadAsync();

            _loaded = true;

            _listener = new TcpListener(IPAddress.Any, _options.Value.Port);

            while (!_stoppingToken.IsCancellationRequested)
            {
                if (_listener.Server.IsBound)
                {
                    AddClient(await _listener.AcceptTcpClientAsync(_stoppingToken));
                }
                else
                {
                    await StartAsync();
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            Stop();
            _storage.Dispose();
            ReadCounter.Dispose();
            WriteCounter.Dispose();
            ReadMessageCounter.Dispose();
            WriteMessageCounter.Dispose();
        }

        private async Task StartAsync()
        {
            var lastError = string.Empty;

            while (true)
            {
                try
                {
                    _listener.Start();

                    _started = true;

                    _logger.LogInformation($"Server listenter started on port {_options.Value.Port}.");

                    break;
                }
                catch (Exception ex)
                {
                    if (lastError != ex.GetBaseException().Message)
                    {
                        _logger.LogError(ex, $"Cannot start server listener on port {_options.Value.Port}. Retry in {StartListenerErrorRetryInterval}.");

                        lastError = ex.GetBaseException().Message;
                    }
                }

                await Task.Delay(StartListenerErrorRetryInterval, _stoppingToken);
            }
        }

        private void Stop()
        {
            _listener.Stop();

            _started = false;

            _logger.LogWarning($"Server listenter stopped on port {_options.Value.Port}.");
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
                
                _storage.AddClient(client);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during adding client.");

                if (client != null)
                {
                    _storage.DeleteClient(client);
                }
            }
        }

        private void OnClientStop(NaiveMqClient sender)
        {
            _storage.DeleteClient(sender);
        }

        private Task OnClientSendCommandAsync(NaiveMqClient sender, ICommand command)
        {
            WriteCounter.Add();
            return Task.CompletedTask;
        }

        private Task OnClientSendMessageAsync(NaiveMqClient sender, Message command)
        {
            WriteMessageCounter.Add();
            return Task.CompletedTask;
        }

        private Task OnClientReceiveCommandAsync(NaiveMqClient sender, ICommand command)
        {
            ReadCounter.Add();
            return Task.CompletedTask;
        }

        private Task OnClientReceiveMessageAsync(NaiveMqClient sender, Message command)
        {
            ReadMessageCounter.Add();
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
                request.Validate();
            
                response = await HandleRequestAsync(sender, request);

                if (response != null)
                {
                    await SendAsync(sender, response);
                }
            }
            catch (ClientException ex)
            {
                await SendError(sender, request, ex.ErrorCode.ToString(), ex.Message);
            }
            catch (ServerException ex)
            {
                await SendError(sender, request, ex.ErrorCode.ToString(), ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Client receive request error.");

                await SendError(sender, request, ErrorCode.UnexpectedCommandHandlerExecutionError.ToString(), ex.GetBaseException().Message);
            }
        }

        private async Task SendError(NaiveMqClient sender, IRequest request, string errorCode, string errorMessage)
        {
            await SendAsync(sender, Confirmation.Error(request.Id, errorCode, errorMessage));
        }

        private async Task SendAsync(NaiveMqClient client, IResponse response)
        {
            try
            {
                await client.SendAsync(response, _stoppingToken);
            }
            catch (ClientException)
            {
                _storage.DeleteClient(client);
            }
            catch (Exception ex)
            {
                _storage.DeleteClient(client);
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
                    _storage.TryGetClient(sender.Id, out var clientContext);

                    instance = (IDisposable)Activator.CreateInstance(commandHandler);
                    var task = (Task)method.Invoke(instance, new object[] { clientContext, command });

                    await task.ConfigureAwait(false);

                    var resultProperty = task.GetType().GetProperty("Result");
                    var result = (IResponse)resultProperty.GetValue(task);

                    return result;
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