using Microsoft.Extensions.Logging;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Converters;
using NaiveMq.Client.Dto;
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
    public class NaiveMqClient : IDisposable
    {
        public const int DefaultPort = 8506;

        public static Dictionary<string, Type> CommandTypes { get; } = new(StringComparer.InvariantCultureIgnoreCase);

        public int Id => GetHashCode();

        public TcpClient TcpClient { get; private set; }

        public bool Started { get; private set; }

        public NaiveMqClientOptions Options { get; }

        public ClientCounters Counters { get; }

        public delegate void OnStartHandler(NaiveMqClient sender);

        public event OnStartHandler OnStart;

        public delegate void OnStopHandler(NaiveMqClient sender);

        public event OnStopHandler OnStop;

        public delegate Task OnReceiveErrorHandler(NaiveMqClient sender, Exception ex);

        public event OnReceiveErrorHandler OnReceiveErrorAsync;

        public delegate Task OnReceiveCommandHandler(NaiveMqClient sender, ICommand command);

        public event OnReceiveCommandHandler OnReceiveCommandAsync;

        public delegate Task OnReceiveRequestHandler(NaiveMqClient sender, IRequest request);

        public event OnReceiveRequestHandler OnReceiveRequestAsync;

        public delegate Task OnReceiveMessageHandler(NaiveMqClient sender, Commands.Message message);

        public event OnReceiveMessageHandler OnReceiveMessageAsync;

        public delegate Task OnReceiveResponseHandler(NaiveMqClient sender, IResponse response);

        public event OnReceiveResponseHandler OnReceiveResponseAsync;

        public delegate Task OnSendCommandHandler(NaiveMqClient sender, ICommand command);

        public event OnSendCommandHandler OnSendCommandAsync;

        public delegate Task OnSendMessageHandler(NaiveMqClient sender, Commands.Message message);

        public event OnSendMessageHandler OnSendMessageAsync;

        private SemaphoreSlim _readSemaphore;

        private readonly ILogger<NaiveMqClient> _logger;

        private readonly CancellationToken _stoppingToken;

        private readonly SemaphoreSlim _writeSemaphore = new(1, 1);

        private readonly ConcurrentDictionary<Guid, ResponseItem> _responses = new();

        private readonly ICommandConverter _converter = new JsonCommandConverter();

        private readonly object _startLocker = new();

        private readonly StringEnumConverter _stringEnumConverter = new();

        private readonly CommandPacker _commandPacker;

        static NaiveMqClient()
        {
            RegisterCommands(typeof(ICommand).Assembly);
        }

        public NaiveMqClient(NaiveMqClientOptions options, ILogger<NaiveMqClient> logger, CancellationToken stoppingToken)
        {
            Options = options;
            Counters = new();

            _logger = logger;
            _stoppingToken = stoppingToken;
            _commandPacker = new CommandPacker(_converter);

            if (options.Autostart)
            {
                Start();
            }
        }

        public static void RegisterCommands(Assembly assembly)
        {
            var types = assembly.GetTypes()
               .Where(x => !x.IsAbstract && !x.IsInterface && x.GetInterfaces().Any(y => y == typeof(ICommand)));

            foreach (var type in types)
            {
                if (CommandTypes.ContainsKey(type.Name))
                {
                    throw new ClientException(ErrorCode.CommandAlreadyRegistered, new object[] { type.Name });
                }
                else
                {
                    CommandTypes.Add(type.Name, type);
                }
            }
        }

        public void Start()
        {
            lock (_startLocker)
            {
                if (!Started)
                {
                    if (Options.TcpClient != null)
                    {
                        TcpClient = Options.TcpClient;
                    }
                    else
                    {
                        CreateTcpClient();
                    }

                    if (TcpClient != null)
                    {
                        _readSemaphore = new(Options.Parallelism, Options.Parallelism);

                        Task.Run(ReceiveAsync);

                        Started = true;

                        OnStart?.Invoke(this);
                    }
                    else
                    {
                        throw new ClientException(ErrorCode.ConnectionParametersAreEmpty);
                    }
                }
            }
        }

        public void Stop()
        {
            lock (_startLocker)
            {
                if (Started)
                {
                    TcpClient.Close();
                    TcpClient.Dispose();
                    TcpClient = null;

                    _readSemaphore.Dispose();
                    _readSemaphore = null;

                    Started = false;

                    OnStop?.Invoke(this);
                }
            }
        }

        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
            where TResponse : IResponse
        {
            return SendAsync(request, true, CancellationToken.None);
        }

        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
            where TResponse : IResponse
        { 
            return SendAsync(request, true, cancellationToken);
        }

        /// <summary>
        /// Send request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="wait"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="ConnectionException"></exception>
        /// <exception cref="ConfirmationException<TResponse>"></exception>
        /// <exception cref="TimeoutException"></exception>
        /// <returns></returns>
        public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, bool wait, CancellationToken cancellationToken)
            where TResponse : IResponse
        {
            await PrepareCommandAsync(request, cancellationToken);
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

                if (request is Commands.Message message && OnSendMessageAsync != null)
                {
                    Counters.Write.Add();
                    await OnSendMessageAsync.Invoke(this, message);
                }

                if (confirmAndWait)
                {
                    response = await WaitForConfirmationAsync(request, responseItem, cancellationToken);
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
            await PrepareCommandAsync(response, cancellationToken);
            response.Validate();
            await WriteCommandAsync(response, cancellationToken);
        }

        public void Dispose()
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

        private void CreateTcpClient()
        {
            var hosts = Host.Parse(Options.Hosts).ToList();

            if (!hosts.Any())
            {
                throw new ClientException(ErrorCode.HostsNotSet);
            }

            do
            {
                var randomHostIndex = hosts.Count == 1 ? 0 : RandomNumberGenerator.GetInt32(0, hosts.Count);
                var host = hosts[randomHostIndex];

                try
                {
                    var tcpClient = new TcpClient();

                    if (tcpClient.ConnectAsync(host.Name, host.Port ?? DefaultPort).Wait(Options.ConnectionTimeout))
                    {
                        TcpClient = tcpClient;
                        return;
                    }
                    else
                    {
                        tcpClient.Dispose();
                    }
                }
                catch (Exception)
                {
                    // ok, taking the next one
                }
                finally
                {
                    hosts.RemoveAt(randomHostIndex);
                }
            } while (hosts.Any());

            throw new ClientException(ErrorCode.HostsUnavailable);
        }

        private async Task PrepareCommandAsync(ICommand command, CancellationToken cancellationToken)
        {
            if (command is IRequest request && request.Confirm && request.ConfirmTimeout == null)
            {
                request.ConfirmTimeout = Options.ConfirmTimeout;
            }

            await command.PrepareAsync(cancellationToken);
        }

        private async Task<IResponse> WaitForConfirmationAsync<TResponse>(IRequest<TResponse> request, ResponseItem responseItem, CancellationToken cancellationToken)
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
                if (!responseItem.Response.Success)
                {
                    ErrorCode errorCode;
                    var parsed = Enum.TryParse(responseItem.Response.ErrorCode, true, out errorCode);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ConnectionException"></exception>
        /// <exception cref="ClientStoppedException"></exception>
        private async Task WriteCommandAsync(ICommand command, CancellationToken cancellationToken)
        {
            PackResult package = null;

            try
            {
                package = _commandPacker.Pack(command, ArrayPool<byte>.Shared);

                await WriteBytesAsync(package.Buffer.AsMemory(0, package.Length), cancellationToken);

                Counters.WriteCommand.Add();

                TraceCommand(">>", command);

                if (OnSendCommandAsync != null)
                {
                    await OnSendCommandAsync.Invoke(this, command);
                }
            }
            catch
            {
                Stop();
                throw;
            }
            finally
            {
                if (package != null)
                {
                    ArrayPool<byte>.Shared.Return(package.Buffer);
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

        private async Task ReceiveAsync()
        {
            while (!_stoppingToken.IsCancellationRequested && Started)
            {
                try
                {
                    var stream = TcpClient?.GetStream();

                    if (stream == null)
                    {
                        throw new IOException("TcpClient is closed.");
                    }

                    var unpackResult = await _commandPacker.Unpack(stream, CheckCommandLengths, _stoppingToken, ArrayPool<byte>.Shared);

                    Counters.ReadCommand.Add();

                    await _readSemaphore.WaitAsync(_stoppingToken);

                    _ = Task.Run(async () => await HandleReceivedDataAsync(unpackResult), _stoppingToken);
                }
                catch (Exception ex)
                {
                    await HandleReceiveErrorAsync(ex);
                    throw;
                }
            };
        }       

        private void CheckCommandLengths(UnpackResult unpackResult)
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

        private async Task HandleReceivedDataAsync(UnpackResult unpackResult)
        {
            try
            {
                var command = _commandPacker.CreateCommand(unpackResult);

                TraceCommand("<<", command);

                await command.RestoreAsync(_stoppingToken);
                command.Validate();

                await HandleReceiveCommandAsync(command);
            }
            catch (Exception ex)
            {
                await HandleReceiveErrorAsync(ex);
                throw;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(unpackResult.Buffer);

                _readSemaphore.Release();
            }
        }

        private async Task HandleReceiveErrorAsync(Exception ex)
        {
            Stop();

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

        private async Task HandleReceiveCommandAsync(ICommand command)
        {
            if (OnReceiveCommandAsync != null)
            {
                await OnReceiveCommandAsync.Invoke(this, command);
            }

            if (command is IRequest request && OnReceiveRequestAsync != null)
            {
                await OnReceiveRequestAsync.Invoke(this, request);
            }

            if (command is IResponse response)
            {
                HandleResponse(response);

                if (OnReceiveResponseAsync != null)
                {
                    await OnReceiveResponseAsync.Invoke(this, response);
                }
            }

            if (command is Commands.Message message && OnReceiveMessageAsync != null)
            {
                Counters.Read.Add();
                await OnReceiveMessageAsync.Invoke(this, message);
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

        private void TraceCommand(string prefix, ICommand command)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var dataCommand = command as IDataCommand;
                _logger.LogTrace($"{prefix} {command.GetType().Name}, {Id}: {JsonConvert.SerializeObject(command, _stringEnumConverter)}{(dataCommand != null ? $", DataLength: {dataCommand.Data.Length}" : string.Empty)}");
            }
        }

        public class ClientCounters : IDisposable
        {
            public SpeedCounters Read { get; }

            public SpeedCounters Write { get; }

            public SpeedCounters ReadCommand { get; }

            public SpeedCounters WriteCommand { get; }

            private SpeedCounterService _service = new();

            public ClientCounters()
            {
                Read = new(_service);
                Write = new(_service);
                ReadCommand = new(_service);
                WriteCommand = new(_service);
            }

            public virtual void Dispose()
            {
                _service.Dispose();
                Read.Dispose();
                Write.Dispose();
                ReadCommand.Dispose();
                WriteCommand.Dispose();
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
