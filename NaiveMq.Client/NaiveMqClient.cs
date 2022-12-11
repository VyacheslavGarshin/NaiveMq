using Microsoft.Extensions.Logging;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Converters;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NaiveMq.Client
{
    public class NaiveMqClient : IDisposable
    {
        private class ResponseItem : IDisposable
        {
            public SemaphoreSlim SemaphoreSlim { get; set; } = new(0, 1);

            public IResponse Response { get; set; }

            public void Dispose()
            {
                SemaphoreSlim.Dispose();
            }
        }

        public int Id => GetHashCode();

        public bool Started => _started;

        public SpeedCounter WriteCounter { get; set; } = new(10);

        public SpeedCounter ReadCounter { get; set; } = new(10);

        public NaiveMqClientOptions Options { get; set; }

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

        public delegate Task OnReceiveMessageHandler(NaiveMqClient sender, Message message);

        public event OnReceiveMessageHandler OnReceiveMessageAsync;

        public delegate Task OnReceiveResponseHandler(NaiveMqClient sender, IResponse response);

        public event OnReceiveResponseHandler OnReceiveResponseAsync;

        public delegate Task OnSendCommandHandler(NaiveMqClient sender, ICommand command);

        public event OnSendCommandHandler OnSendCommandAsync;

        public delegate Task OnSendMessageHandler(NaiveMqClient sender, Message message);

        public event OnSendMessageHandler OnSendMessageAsync;

        private static readonly Dictionary<string, Type> _commandTypes = new();

        private TcpClient _tcpClient { get; set; }

        private bool _started;

        private SemaphoreSlim _readSemaphore;
        
        private readonly ILogger<NaiveMqClient> _logger;

        private readonly CancellationToken _stoppingToken;

        private readonly SemaphoreSlim _writeSemaphore = new(1, 1);

        private readonly ConcurrentDictionary<Guid, ResponseItem> _responses = new();

        private readonly ICommandConverter _converter = new JsonCommandConverter();
        
        private readonly object _startLocker = new();

        static NaiveMqClient()
        {
            foreach (var type in typeof(ICommand).Assembly.GetTypes().Where(x => x.GetInterfaces().Any(y => y == typeof(ICommand))))
            {
                _commandTypes.Add(type.Name, type);
            }
        }

        public NaiveMqClient(NaiveMqClientOptions options, ILogger<NaiveMqClient> logger, CancellationToken stoppingToken)
        {
            Options = options;
            
            _logger = logger;
            _stoppingToken = stoppingToken;

            Start();
        }

        public void Start()
        {
            lock (_startLocker)
            {
                if (!_started)
                {
                    if (Options.TcpClient != null)
                    {
                        _tcpClient = Options.TcpClient;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(Options.Host) && Options.Port > 0)
                        {
                            _tcpClient = new TcpClient(Options.Host, Options.Port);
                        }
                    }

                    if (_tcpClient != null)
                    {
                        _readSemaphore = new(Options.Parallelism, Options.Parallelism);

                        Task.Run(ReceiveAsync);

                        _started = true;

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
                if (_started)
                {
                    _tcpClient.Dispose();
                    _tcpClient = null;

                    _readSemaphore.Dispose();
                    _readSemaphore = null;

                    _started = false;

                    OnStop?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// Send request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="ConnectionException"></exception>
        /// <exception cref="ConfirmationException<TResponse>"></exception>
        /// <exception cref="TimeoutException"></exception>
        /// <returns></returns>
        public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
            where TResponse : IResponse
        {
            PrepareCommand(request);
            request.Validate();

            IResponse response = null;
            ResponseItem responseItem = null;
            var confirm = request.Confirm;

            try
            {

                if (confirm)
                {
                    responseItem = new ResponseItem();
                    _responses[request.Id] = responseItem;
                }

                await WriteCommandAsync(request, cancellationToken);

                if (request is Message message && OnSendMessageAsync != null)
                {
                    await OnSendMessageAsync.Invoke(this, message);
                }

                if (confirm)
                {
                    response = await WaitForConfirmation(request, responseItem, cancellationToken);
                }
            }
            finally
            {
                if (confirm)
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
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task SendAsync(IResponse response, CancellationToken cancellationToken)
        {
            PrepareCommand(response);
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

            ReadCounter.Dispose();
            WriteCounter.Dispose();
        }

        private void PrepareCommand(ICommand command)
        {
            if (command.Id == Guid.Empty)
            {
                command.Id = Guid.NewGuid();
            }

            if (command is IRequest request && request.Confirm && request.ConfirmTimeout == null)
            {
                request.ConfirmTimeout = Options.ConfirmTimeout;
            }
        }

        private async Task<IResponse> WaitForConfirmation<TResponse>(IRequest<TResponse> request, ResponseItem responseItem, CancellationToken cancellationToken)
            where TResponse : IResponse
        {
            IResponse response;
            
            var entered = await responseItem.SemaphoreSlim.WaitAsync((int)(request.ConfirmTimeout ?? Options.ConfirmTimeout).TotalMilliseconds, cancellationToken);

            if (!entered)
            {
                throw new ClientException(_started ? ErrorCode.ConfirmationTimeout : ErrorCode.ClientStopped);
            }   
            else
            {
                if (!responseItem.Response.Success)
                {
                    throw new ClientException(ErrorCode.ConfirmationError, $"{responseItem.Response.ErrorCode}: {responseItem.Response.ErrorMessage}");
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
            byte[] bytes = null;

            try
            {
                var commandNameBytes = Encoding.UTF8.GetBytes(command.GetType().Name);
                var commandBytes = _converter.Serialize(command);
                var dataLength = 0;
                var data = new ReadOnlyMemory<byte>();

                if (command is IDataCommand dataCommand)
                {
                    dataLength = dataCommand.Data.Length;
                    data = dataCommand.Data;
                }

                var bytesLength = 4 * 3 + commandNameBytes.Length + commandBytes.Length + dataLength;
                bytes = ArrayPool<byte>.Shared.Rent(bytesLength);

                bytes.CopyFrom(new[] {
                    BitConverter.GetBytes(commandNameBytes.Length),
                    BitConverter.GetBytes(commandBytes.Length),
                    BitConverter.GetBytes(dataLength),
                    commandNameBytes,
                    commandBytes,
                    data });

                await WriteBytesAsync(bytes.AsMemory(0, bytesLength), cancellationToken);

                WriteCounter.Add();

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
                if (bytes != null)
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                }
            }
        }

        private async Task WriteBytesAsync(Memory<byte> bytes, CancellationToken cancellationToken)
        {
            if (!_started)
            {
                throw new ClientException(ErrorCode.ClientStopped);
            }

            var semaphoreEntered = false;

            try
            {
                try
                {
                    semaphoreEntered = await _writeSemaphore.WaitAsync(Options.SendTimeout, cancellationToken);
                }
                catch
                {
                    throw;
                }

                var stream = _tcpClient?.GetStream();
                
                if (!_started || stream == null)
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
            while (!_stoppingToken.IsCancellationRequested && _started)
            {
                try
                {
                    var stream = _tcpClient?.GetStream();

                    if (stream == null)
                    {
                        throw new IOException("TcpClient is closed.");
                    }

                    var lengthsBytes = await ReadContentAsync(stream, 4 * 3);

                    var commandNameLength = BitConverter.ToInt32(lengthsBytes, 0);
                    var commandLength = BitConverter.ToInt32(lengthsBytes, 4);
                    var dataLength = BitConverter.ToInt32(lengthsBytes, 8);

                    CheckCommandLengths(commandNameLength, commandLength, dataLength);

                    var commandNameBytes = await ReadContentAsync(stream, commandNameLength);
                    var commandBytes = await ReadContentAsync(stream, commandLength);
                    var dataBytes = dataLength > 0 ? await ReadContentAsync(stream, dataLength) : Array.Empty<byte>();

                    await _readSemaphore.WaitAsync(_stoppingToken);

                    _ = Task.Run(async () => await HandleReceivedDataAsync(commandNameBytes, commandBytes, dataBytes), _stoppingToken);
                }
                catch (Exception ex)
                {
                    await HandleReceiveErrorAsync(ex);
                    throw;
                }
            };
        }

        private void CheckCommandLengths(int commandNameLength, int commandLength, int dataLength)
        {
            if (commandNameLength == 0)
            {
                throw new IOException("Incoming command length is 0. Looks like the other side dropped the connection.");
            }

            if (commandNameLength > Options.MaxCommandNameLength)
            {
                throw new ClientException(ErrorCode.CommandNameLengthLong, string.Format(ErrorCode.CommandNameLengthLong.GetDescription(), Options.MaxCommandNameLength, commandNameLength));
            }

            if (commandLength > Options.MaxCommandLength)
            {
                throw new ClientException(ErrorCode.CommandLengthLong, string.Format(ErrorCode.CommandLengthLong.GetDescription(), Options.MaxCommandLength, commandNameLength));
            }

            if (dataLength > Options.MaxDataLength)
            {
                throw new ClientException(ErrorCode.DataLengthLong, string.Format(ErrorCode.DataLengthLong.GetDescription(), Options.MaxDataLength, commandNameLength));
            }
        }

        private async Task<byte[]> ReadContentAsync(NetworkStream stream, int length)
        {
            var bytes = new byte[length];
            await ReadAllLength(stream, bytes);
            return bytes;
        }

        private async Task ReadAllLength(NetworkStream stream, byte[] bytes, int? length = null)
        {
            var desiredSize = length ?? bytes.Length;
            var readLength = 0;
            var offset = 0;
            var size = desiredSize;

            do
            {
                var read = await stream.ReadAsync(bytes, offset, size, _stoppingToken);
                
                readLength += read;
                offset += read;
                size -= read;
            } while (readLength != desiredSize);
        }

        private async Task HandleReceivedDataAsync(byte[] commandNameBytes, byte[] commandBytes, byte[] dataBytes)
        {
            try
            {
                ReadCounter.Add();

                var commandName = Encoding.UTF8.GetString(commandNameBytes);
                var commandType = GetCommandType(commandName);

                var command = ParseCommand(commandBytes, commandType);

                if (command is IDataCommand dataCommand)
                {
                    dataCommand.Data = dataBytes;
                }

                TraceCommand("<<", command);

                await HandleReceiveCommandAsync(command);
            }
            catch (Exception ex)
            {
                await HandleReceiveErrorAsync(ex);
                throw;
            }
            finally
            {
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

            if (command is Message message && OnReceiveMessageAsync != null)
            {
                await OnReceiveMessageAsync.Invoke(this, message);
            }
        }

        private void HandleResponse(IResponse response)
        {
            if (_responses.TryGetValue(response.RequestId, out var responseItem))
            {
                responseItem.Response = response;
                responseItem.SemaphoreSlim.Release();
            }
        }

        private Type GetCommandType(string commandName)
        {
            if (_commandTypes.TryGetValue(commandName, out Type commandType))
            {
                return commandType;
            }
            else
            {
                throw new ClientException(ErrorCode.CommandNotFound, string.Format(ErrorCode.CommandNotFound.GetDescription(), commandName));
            }
        }

        private ICommand ParseCommand(byte[] commandBytes, Type commandType)
        {
            var result = _converter.Deserialize(commandBytes, commandType);

            if (result.Id == Guid.Empty)
            {
                throw new ClientException(ErrorCode.EmptyCommandId);
            }

            return result;
        }

        private void TraceCommand(string prefix, ICommand command)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"{prefix} {command.GetType().Name}, {Id}: {JsonConvert.SerializeObject(command)}");
            }
        }
    }
}
