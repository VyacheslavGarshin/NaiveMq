using Microsoft.Extensions.Logging;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
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

        public bool IsStarted => _isStarted;

        public SpeedCounter WriteCounter { get; set; } = new(10);

        public SpeedCounter ReadCounter { get; set; } = new(10);

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

        private bool _isStarted;

        private SemaphoreSlim _readSemaphore;
        
        private readonly ILogger<NaiveMqClient> _logger;

        private readonly CancellationToken _stoppingToken;

        private readonly SemaphoreSlim _writeSemaphore = new(1, 1);

        private readonly ConcurrentDictionary<Guid, ResponseItem> _responses = new();

        private readonly NaiveMqClientOptions _options;

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
            _options = options;
            _logger = logger;
            _stoppingToken = stoppingToken;

            Start();
        }

        public void Start()
        {
            lock (_startLocker)
            {
                if (!_isStarted)
                {
                    if (_options.TcpClient != null)
                    {
                        _tcpClient = _options.TcpClient;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(_options.Host) && _options.Port > 0)
                        {
                            _tcpClient = new TcpClient(_options.Host, _options.Port);
                        }
                    }

                    if (_tcpClient != null)
                    {
                        _readSemaphore = new(_options.Parallelism, _options.Parallelism);

                        Task.Run(ReceiveAsync);

                        _isStarted = true;
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
                if (_isStarted)
                {
                    _tcpClient.Dispose();
                    _tcpClient = null;

                    _readSemaphore.Dispose();
                    _readSemaphore = null;

                    _isStarted = false;
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
            IResponse response = null;
            ResponseItem responseItem = null;
            var confirm = request.Confirm;

            try
            {
                PrepareCommand(request);

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

        private static void PrepareCommand(ICommand command)
        {
            if (command.Id == Guid.Empty)
            {
                command.Id = Guid.NewGuid();
            }
        }

        private async Task<IResponse> WaitForConfirmation<TResponse>(IRequest<TResponse> request, ResponseItem responseItem, CancellationToken cancellationToken)
            where TResponse : IResponse
        {
            IResponse response;
            
            var entered = await responseItem.SemaphoreSlim.WaitAsync((int)(request.ConfirmTimeout ?? _options.ConfirmTimeout).TotalMilliseconds, cancellationToken);

            if (!entered)
            {
                if (_isStarted)
                {
                    throw new ClientException(ErrorCode.ClientStopped);
                }
                else
                {
                    throw new ClientException(ErrorCode.ConfirmationTimeout);
                }
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
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            try
            {
                var commandNameBytes = Encoding.UTF8.GetBytes(command.GetType().Name);
                var commandBytes = _converter.Serialize(command);
                var dataLength = 0;
                var data = Array.Empty<byte>();

                if (command is IDataCommand dataCommand && dataCommand.Data != null)
                {
                    dataLength = dataCommand.Data.Length;
                    data = dataCommand.Data;
                }

                var bytes = BitConverter.GetBytes(commandNameBytes.Length)
                    .Concat(commandNameBytes)
                    .Concat(BitConverter.GetBytes(commandBytes.Length))
                    .Concat(commandBytes)
                    .Concat(BitConverter.GetBytes(dataLength))
                    .Concat(data)
                    .ToArray();


                await WriteBytesAsync(bytes, cancellationToken);

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
        }

        private async Task WriteBytesAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            if (!_isStarted)
            {
                throw new ClientException(ErrorCode.ClientStopped);
            }

            var semaphoreEntered = false;

            try
            {
                try
                {
                    semaphoreEntered = await _writeSemaphore.WaitAsync(_options.SendTimeout, cancellationToken);
                }
                catch
                {
                    throw;
                }

                var stream = _tcpClient?.GetStream();
                
                if (!_isStarted || stream == null)
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
            while (!_stoppingToken.IsCancellationRequested && _isStarted)
            {
                try
                {
                    var stream = _tcpClient?.GetStream();

                    if (stream == null)
                    {
                        throw new IOException("TcpClient is closed.");
                    }

                    var commandNameLength = await ReadLengthAsync(stream);

                    if (commandNameLength == 0)
                    {
                        throw new IOException("Incoming command length is 0. Looks like the other side dropped the connection.");
                    }

                    var commandNameBytes = await ReadContentAsync(stream, commandNameLength);

                    var commandLength = await ReadLengthAsync(stream);
                    var commandBytes = await ReadContentAsync(stream, commandLength);

                    var dataLength = await ReadLengthAsync(stream);
                    var dataBytes = dataLength > 0 ? await ReadContentAsync(stream, dataLength) : Array.Empty<byte>();

                    var commandName = Encoding.UTF8.GetString(commandNameBytes);

                    await _readSemaphore.WaitAsync(_stoppingToken);

                    _ = Task.Run(async () => await HandleReceivedDataAsync(commandName, commandBytes, dataBytes), _stoppingToken);
                }
                catch (Exception ex)
                {
                    await HandleReceiveErrorAsync(ex);
                    throw;
                }
            };
        }

        private async Task<int> ReadLengthAsync(NetworkStream stream)
        {
            var bytes = new byte[4];
            await stream.ReadAsync(bytes, 0, 4, _stoppingToken);
            return BitConverter.ToInt32(bytes);
        }

        private async Task<byte[]> ReadContentAsync(NetworkStream stream, int length)
        {
            var bytes = new byte[length];
            await stream.ReadAsync(bytes, 0, length, _stoppingToken);
            return bytes;
        }

        private async Task HandleReceivedDataAsync(string commandName, byte[] commandBytes, byte[] dataBytes)
        {
            try
            {
                ReadCounter.Add();

                var command = ParseMessage(commandName, commandBytes);

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

        private ICommand ParseMessage(string commandName, byte[] commandBytes)
        {
            if (_commandTypes.TryGetValue(commandName, out Type commandType))
            {
                return ParseCommand(commandBytes, commandType);
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
