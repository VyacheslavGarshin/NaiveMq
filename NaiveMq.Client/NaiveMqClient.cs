using Microsoft.Extensions.Logging;
using NaiveMq.Client.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;
using NaiveMq.Client.Serializers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace NaiveMq.Client
{
    /// <summary>
    /// NaiveMq client.
    /// </summary>
    public class NaiveMqClient : IDisposable
    {
        /// <summary>
        /// Default port is 8506.
        /// </summary>
        public const int DefaultPort = 8506;

        /// <summary>
        /// Registered commands.
        /// </summary>
        public static readonly CommandRegistry Commands = new();

        /// <summary>
        /// Registered serializers.
        /// </summary>
        public static Dictionary<string, Type> CommandSerializers { get; } = new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Id of the client.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Current TcpClient.
        /// </summary>
        public TcpClient TcpClient { get; private set; }

        /// <summary>
        /// Is started.
        /// </summary>
        public bool Started { get; private set; }

        /// <summary>
        /// Options.
        /// </summary>
        public NaiveMqClientOptions Options { get; }

        /// <summary>
        /// Counters.
        /// </summary>
        public ClientCounters Counters { get; }

        /// <summary>
        /// OnStartHandler.
        /// </summary>
        /// <param name="sender"></param>
        public delegate void OnStartHandler(NaiveMqClient sender);

        /// <summary>
        /// On start.
        /// </summary>
        public event OnStartHandler OnStart;

        /// <summary>
        /// OnStopHandler.
        /// </summary>
        /// <param name="sender"></param>
        public delegate void OnStopHandler(NaiveMqClient sender);

        /// <summary>
        /// On stop.
        /// </summary>
        public event OnStopHandler OnStop;

        /// <summary>
        /// OnReceiveErrorHandler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        public delegate Task OnReceiveErrorHandler(NaiveMqClient sender, Exception ex);

        /// <summary>
        /// On receive error async.
        /// </summary>
        public event OnReceiveErrorHandler OnReceiveErrorAsync;

        /// <summary>
        /// OnReceiveCommandHandler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        public delegate Task OnReceiveCommandHandler(NaiveMqClient sender, ICommand command);

        /// <summary>
        /// On receive command async.
        /// </summary>
        public event OnReceiveCommandHandler OnReceiveCommandAsync;

        /// <summary>
        /// OnReceiveRequestHandler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public delegate Task OnReceiveRequestHandler(NaiveMqClient sender, IRequest request);

        /// <summary>
        /// On receive request async.
        /// </summary>
        public event OnReceiveRequestHandler OnReceiveRequestAsync;

        /// <summary>
        /// OnReceiveMessageHandler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public delegate Task OnReceiveMessageHandler(NaiveMqClient sender, Message message);

        /// <summary>
        /// On receive message async.
        /// </summary>
        public event OnReceiveMessageHandler OnReceiveMessageAsync;

        /// <summary>
        /// OnReceiveResponseHandler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        public delegate Task OnReceiveResponseHandler(NaiveMqClient sender, IResponse response);

        /// <summary>
        /// On receive response async.
        /// </summary>
        public event OnReceiveResponseHandler OnReceiveResponseAsync;

        /// <summary>
        /// OnSendCommandHandler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        public delegate Task OnSendCommandHandler(NaiveMqClient sender, ICommand command);

        /// <summary>
        /// On send command async.
        /// </summary>
        public event OnSendCommandHandler OnSendCommandAsync;

        /// <summary>
        /// OnSendMessageHandler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public delegate Task OnSendMessageHandler(NaiveMqClient sender, Message message);

        /// <summary>
        /// On send message async.
        /// </summary>
        public event OnSendMessageHandler OnSendMessageAsync;

        private static readonly JsonSerializerSettings _traceJsonSettings;

        private SemaphoreSlim _readSemaphore;
        
        private string _host;
        
        private bool _redirecting;

        private Task _receivingTask;

        private CancellationTokenSource _receivingTaskCancellationTokenSource;

        private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Create();
        
        private readonly ILogger<NaiveMqClient> _logger;

        private readonly CancellationToken _stoppingToken;

        private readonly SemaphoreSlim _writeSemaphore = new(1, 1);

        private readonly ConcurrentDictionary<Guid, ResponseItem> _responses = new();

        private readonly ICommandSerializer _commandSerializer;

        private readonly object _startLocker = new();

        private readonly CommandPacker _commandPacker;

        static NaiveMqClient()
        {
            _traceJsonSettings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter>
                {
                    new StringEnumConverter()
                },
                ContractResolver = JsonOriginalPropertiesResolver.Default,
            };

            CommandSerializers.Add(nameof(NaiveCommandSerializer), typeof(NaiveCommandSerializer));
            CommandSerializers.Add(nameof(JsonCommandSerializer), typeof(JsonCommandSerializer));

            RegisterCommands(typeof(ICommand).Assembly);
        }

        /// <summary>
        /// Creates new NaiveMq сlient.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        /// <param name="stoppingToken"></param>
        public NaiveMqClient(NaiveMqClientOptions options, ILogger<NaiveMqClient> logger = null, CancellationToken? stoppingToken = null)
        {
            Id = GetHashCode();
            Options = options;
            Counters = new();

            _logger = logger ?? CreateLogger();
            _stoppingToken = stoppingToken ?? CancellationToken.None;
            _commandSerializer = (ICommandSerializer)Activator.CreateInstance(CommandSerializers[options.CommandSerializer]);
            _commandPacker = new CommandPacker(_commandSerializer, _arrayPool);

            if (Options.OnStart != null)
            {
                OnStart += Options.OnStart;
            }

            if (Options.OnStop != null)
            {
                OnStop += Options.OnStop;
            }

            if (Options.AutoStart)
            {
                Start(true);
            }
        }

        /// <summary>
        /// Register additional commands.
        /// </summary>
        /// <param name="assembly"></param>
        /// <exception cref="ClientException"></exception>
        public static void RegisterCommands(Assembly assembly)
        {
            var types = assembly.GetTypes()
               .Where(x => !x.IsAbstract && !x.IsInterface && x.GetInterfaces().Any(y => y == typeof(ICommand)));

            foreach (var type in types)
            {
                Commands.Add(type);
            }
        }

        /// <summary>
        /// Start client.
        /// </summary>
        /// <param name="login"></param>
        /// <param name="host"></param>
        /// <exception cref="ClientException"></exception>
        public void Start(bool login = false, string host = null)
        {
            lock (_startLocker)
            {
                if (!Started)
                {
                    if (Options.TcpClient != null)
                    {
                        TcpClient = Options.TcpClient;
                        _host = TcpClient.Client.RemoteEndPoint.ToString();
                    }
                    else
                    {
                        _host = CreateTcpClient(host);
                    }

                    if (TcpClient != null)
                    {
                        _readSemaphore = new(Options.Parallelism, Options.Parallelism);

                        Started = true;

                        _receivingTaskCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken);
                        var token = _receivingTaskCancellationTokenSource.Token;
                        _receivingTask = Task.Run(() => ReceiveAsync(TcpClient, _receivingTaskCancellationTokenSource), token);

                        Trace("..", () => $"Connected {(Options.TcpClient != null ? "from" : "to")} {_host}");

                        try
                        {
                            if (login)
                            {
                                SendAsync(new Login(Options.Username, Options.Password)).Wait();
                            }

                            OnStart?.Invoke(this);
                        }
                        catch
                        {
                            DisposeTcpClientAndCancelReceivingTask();

                            throw;
                        }
                    }
                    else
                    {
                        throw new ClientException(ErrorCode.ConnectionParametersAreEmpty);
                    }
                }
            }
        }

        /// <summary>
        /// Stop client.
        /// </summary>
        public void Stop()
        {
            lock (_startLocker)
            {
                if (Started)
                {
                    DisposeTcpClientAndCancelReceivingTask();

                    CancelResponses();

                    OnStop?.Invoke(this);

                    Trace("..", () => $"Stopped connection {(Options.TcpClient != null ? "from" : "to")} {_host}");
                }
            }
        }

        /// <summary>
        /// Send request with wait and throwIfError parameters set to true.
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="request"></param>
        /// <returns></returns>
        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
            where TResponse : IResponse
        {
            return SendAsync(request, true, true, CancellationToken.None);
        }

        /// <summary>
        /// Send request with wait and throwIfError parameters set to true.
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
            where TResponse : IResponse
        { 
            return SendAsync(request, true, true, cancellationToken);
        }

        /// <summary>
        /// Send request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="wait">Wait for response if <see cref="IRequest.Confirm"/> is set to true.</param>
        /// <param name="throwIfError">Throw <see cref="ClientException"/> if <see cref="IResponse.Success"/> is false.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, bool wait, bool throwIfError, CancellationToken cancellationToken)
            where TResponse : IResponse
        {
            PrepareCommand(request);
            request.Validate();

            IResponse response = null;
            ResponseItem responseItem = null;
            var confirmAndWait = request.Confirm && wait;

            try
            {
                if (confirmAndWait)
                {
                    responseItem = new ResponseItem();
                    _responses[request.Id] = responseItem;
                }

                await WriteCommandAsync(request, cancellationToken);

                if (request is Message message && OnSendMessageAsync != null)
                {
                    Counters.Write.Add();
                    await OnSendMessageAsync.Invoke(this, message);
                }

                if (confirmAndWait)
                {
                    response = await WaitForConfirmationAsync(request, responseItem, throwIfError, cancellationToken);
                }
            }
            finally
            {
                if (confirmAndWait)
                {
                    _responses.TryRemove(request.Id, out var _);
                    responseItem.Dispose();
                }
            }

            return (TResponse)response;
        }

        /// <summary>
        /// Send response.
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        public Task SendAsync(IResponse response)
        {
            return SendAsync(response, CancellationToken.None);
        }

        /// <summary>
        /// Send response.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task SendAsync(IResponse response, CancellationToken cancellationToken)
        {
            PrepareCommand(response);
            response.Validate();
            await WriteCommandAsync(response, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            Stop();

            _writeSemaphore.Dispose();

            foreach (var item in _responses.Values)
            {
                item.Dispose();
            }

            _responses.Clear();

            Counters.Dispose();
        }

        private ILogger<NaiveMqClient> CreateLogger()
        {
            var factory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("NaiveMq.Client.NaiveMqClient", LogLevel.Warning)
                    .AddConsole();
            });

            return factory.CreateLogger<NaiveMqClient>();
        }

        private void DisposeTcpClientAndCancelReceivingTask()
        {
            Started = false;

            if (TcpClient != null)
            {
                TcpClient.Dispose();
                TcpClient = null;
            }

            if (_readSemaphore != null)
            {
                _readSemaphore.Dispose();
                _readSemaphore = null;
            }

            if (_receivingTaskCancellationTokenSource != null)
            {
                try
                {
                    _receivingTaskCancellationTokenSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // could be disposed on clent error
                }

                _receivingTaskCancellationTokenSource = null;
            }

            _receivingTask = null;
        }

        private void CancelResponses()
        {
            foreach (var pair in _responses.ToList())
            {
                try
                {
                    pair.Value.Response = new Confirmation
                    {
                        RequestId = pair.Key,
                        ErrorCode = ErrorCode.ClientStopped.ToString(),
                        ErrorMessage = ErrorCode.ClientStopped.GetDescription()
                    };

                    pair.Value.SemaphoreSlim.Release();
                }
                catch (SemaphoreFullException)
                {
                }
                catch (ObjectDisposedException)
                {
                    // it's ok that semaphore is already released
                }
            }
        }

        private void AutoRestart()
        {
            if (Options.AutoRestart)
            {
                Task.Run(async () =>
                {
                    while (!Started)
                    {
                        await Task.Delay(Options.RestartInterval);

                        Trace("..", () => $"Trying to reconnect");

                        try
                        {
                            Start(true);
                        }
                        catch
                        {
                            // insist until success
                        }
                    };
                });
            }
        }

        private string CreateTcpClient(string host = null)
        {
            var hosts = Host.Parse(host ?? Options.Hosts).ToList();

            if (!hosts.Any())
            {
                throw new ClientException(ErrorCode.HostsNotSet);
            }

            Exception lastException = null;

            do
            {
                var randomHostIndex = hosts.Count == 1 ? 0 : RandomNumberGenerator.GetInt32(0, hosts.Count);
                var randomHost = hosts[randomHostIndex];

                try
                {
                    var tcpClient = new TcpClient();

                    if (tcpClient.ConnectAsync(randomHost.Name, randomHost.Port ?? DefaultPort).Wait(Options.ConnectionTimeout))
                    {
                        TcpClient = tcpClient;
                        return randomHost.ToString();
                    }
                    else
                    {
                        tcpClient.Dispose();
                        lastException = new TimeoutException($"Cannot connect to {randomHost} with timeout {Options.ConnectionTimeout}.");
                    }
                }
                catch (Exception ex)
                {
                    // ok, taking the next one
                    lastException = ex;
                }
                finally
                {
                    hosts.RemoveAt(randomHostIndex);
                }
            } while (hosts.Any());

            throw new ClientException(ErrorCode.HostsUnavailable, lastException);
        }

        private void PrepareCommand(ICommand command)
        {
            if (command is IRequest request && request.Confirm && request.ConfirmTimeout == null)
            {
                request.ConfirmTimeout = Options.ConfirmTimeout;
            }

            command.Prepare(_commandPacker);
        }

        private async Task<IResponse> WaitForConfirmationAsync<TResponse>(IRequest<TResponse> request, ResponseItem responseItem, bool throwIfError, CancellationToken cancellationToken)
            where TResponse : IResponse
        {
            IResponse response;
            
            var entered = await responseItem.SemaphoreSlim.WaitAsync((int)(request.ConfirmTimeout ?? Options.ConfirmTimeout).TotalMilliseconds, cancellationToken);

            if (!entered)
            {
                throw new ClientException(Started ? ErrorCode.ConfirmationTimeout : ErrorCode.ClientStopped);
            }   
            else
            {
                if (!responseItem.Response.Success && throwIfError)
                {
                    var parsed = Enum.TryParse(responseItem.Response.ErrorCode, true, out ErrorCode errorCode);
                    throw new ClientException(parsed ? errorCode : ErrorCode.ConfirmationError, responseItem.Response.ErrorMessage)
                    {
                        Response = responseItem.Response
                    };
                }
                else
                {
                    response = responseItem.Response;
                }
            }

            return response;
        }

        private async Task WriteCommandAsync(ICommand command, CancellationToken cancellationToken)
        {
            BufferResult package = null;

            try
            {
                package = _commandPacker.Pack(command);

                await WriteBytesAsync(package.Buffer.AsMemory(0, package.Length), cancellationToken);

                Counters.WriteCommand.Add();

                TraceCommand(">>", command);

                if (OnSendCommandAsync != null)
                {
                    await OnSendCommandAsync.Invoke(this, command);
                }
            }
            catch (Exception ex)
            {
                if (!_redirecting && Started)
                {
                    Stop();
                    AutoRestart();
                }

                throw new ClientException(ErrorCode.ClientStopped, ex);
            }
            finally
            {
                if (package != null)
                {
                    _commandPacker.ArrayPool.Return(package.Buffer);
                }
            }
        }

        private async Task WriteBytesAsync(Memory<byte> bytes, CancellationToken cancellationToken)
        {
            if (!Started)
            {
                throw new ClientException(ErrorCode.ClientStopped);
            }

            var semaphoreEntered = false;

            try
            {
                semaphoreEntered = await _writeSemaphore.WaitAsync(Options.SendTimeout, cancellationToken);

                var stream = TcpClient?.GetStream();

                if (!Started || stream == null)
                {
                    throw new ClientException(ErrorCode.ClientStopped);
                }

                await stream.WriteAsync(bytes, cancellationToken);
            }
            finally
            {
                if (semaphoreEntered)
                {
                    _writeSemaphore.Release();
                }
            }
        }

        private async Task ReceiveAsync(TcpClient tcpClient, CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                var stream = tcpClient.GetStream();

                while (!cancellationTokenSource.Token.IsCancellationRequested && Started)
                {
                    try
                    {
                        var unpackResult = await _commandPacker.ReadAsync(stream, CheckCommandLengths, cancellationTokenSource.Token);

                        Counters.ReadCommand.Add();

                        await _readSemaphore.WaitAsync(cancellationTokenSource.Token);

                        _ = Task.Run(async () => await HandleReceivedDataAsync(tcpClient, unpackResult), cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        await HandleReceiveErrorAsync(tcpClient, ex);
                        throw;
                    }
                };
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }

        private void CheckCommandLengths(CommandReadResult unpackResult)
        {
            if (unpackResult.CommandNameLength == 0)
            {
                throw new IOException("Incoming command length is 0. Looks like the other side dropped the connection.");
            }

            if (unpackResult.CommandNameLength > Options.MaxCommandNameSize)
            {
                throw new ClientException(ErrorCode.CommandNameLengthLong, new object[] { Options.MaxCommandNameSize, unpackResult.CommandNameLength });
            }

            if (unpackResult.CommandLength > Options.MaxCommandSize)
            {
                throw new ClientException(ErrorCode.CommandLengthLong, new object[] { Options.MaxCommandSize, unpackResult.CommandLength });
            }

            if (unpackResult.DataLength > Options.MaxDataSize)
            {
                throw new ClientException(ErrorCode.DataLengthLong, new object[] { Options.MaxDataSize, unpackResult.DataLength });
            }
        }

        private async Task HandleReceivedDataAsync(TcpClient tcpClient, CommandReadResult unpackResult)
        {
            try
            {
                var command = _commandPacker.Unpack(unpackResult);

                TraceCommand("<<", command);

                command.Restore(_commandPacker);
                command.Validate();

                await HandleReceiveCommandAsync(command);
            }
            catch (Exception ex)
            {
                await HandleReceiveErrorAsync(tcpClient, ex);
                throw;
            }
            finally
            {
                _readSemaphore?.Release();

                _commandPacker.ArrayPool.Return(unpackResult.Buffer);
            }
        }

        private async Task HandleReceiveErrorAsync(TcpClient tcpClient, Exception ex)
        {
            try
            {
                if (tcpClient == TcpClient && !_redirecting && Started)
                {
                    Stop();
                    AutoRestart();

                    if (ex is TaskCanceledException || ex is IOException || ex is OperationCanceledException
                        || ex is ObjectDisposedException)
                    {
                        return;
                    }

                    _logger.LogError(ex, "Error occured during handling an incoming command.");

                    if (OnReceiveErrorAsync != null)
                    {
                        await OnReceiveErrorAsync.Invoke(this, ex);
                    }
                }
            }
            catch (Exception handlingEx)
            {
                _logger.LogError(handlingEx, "Error handling receiving error.");
                throw;
            }
        }

        private async Task HandleReceiveCommandAsync(ICommand command)
        {
            if (OnReceiveCommandAsync != null)
            {
                await OnReceiveCommandAsync.Invoke(this, command);
            }

            if (command is IRequest request)
            {
                if (OnReceiveRequestAsync != null)
                {
                    await OnReceiveRequestAsync.Invoke(this, request);
                }
            }
            else if (command is IResponse response)
            {
                HandleResponse(response);

                if (OnReceiveResponseAsync != null)
                {
                    await OnReceiveResponseAsync.Invoke(this, response);
                }
            }

            if (command is Message message)
            {
                Counters.Read.Add();

                if (OnReceiveMessageAsync != null)
                {
                    await OnReceiveMessageAsync.Invoke(this, message);
                }
            }
            else if (command is ClusterRedirect clusterRedirect)
            {
                ClusterRedirect(clusterRedirect);
            }
        }

        private void HandleResponse(IResponse response)
        {
            if (_responses.TryGetValue(response.RequestId, out var responseItem))
            {
                if (response is IDataCommand dataCommand)
                {
                    // materialize data from buffer
                    dataCommand.Data = dataCommand.Data.ToArray();
                }

                responseItem.Response = response;
                responseItem.SemaphoreSlim.Release();
            }
        }

        private void ClusterRedirect(ClusterRedirect clusterRedirect)
        {
            if (Options.AutoClusterRedirect)
            {
                _redirecting = true;

                try
                {
                    Stop();
                    Start(true, clusterRedirect.Host);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during cluster redirection.");
                }
                finally
                {
                    _redirecting = false;
                }

                if (!Started)
                {
                    AutoRestart();
                }
            }
        }

        private void Trace(string prefix, Func<string> func)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"{prefix} {func()}, {Id}");
            }
        }

        private void TraceCommand(string prefix, ICommand command)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var json = JsonConvert.SerializeObject(command, _traceJsonSettings);
                _logger.LogTrace($"{prefix} {command.GetType().Name}, {Id}: {json}{(command is IDataCommand dataCommand ? $", DataLength: {dataCommand.Data.Length}" : string.Empty)}");
            }
        }

        private class ResponseItem : IDisposable
        {
            public SemaphoreSlim SemaphoreSlim { get; set; } = new(0, 1);

            public IResponse Response { get; set; }

            public void Dispose()
            {
                SemaphoreSlim.Dispose();
            }
        }
    }
}
