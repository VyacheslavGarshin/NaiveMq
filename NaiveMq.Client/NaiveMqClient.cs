using Microsoft.Extensions.Logging;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Converters;
using NaiveMq.Client.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

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

        public TcpClient TcpClient { get; set; }

        public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromSeconds(60);

        public SpeedCounter WriteCounter { get; set; } = new(10);

        public SpeedCounter ReadCounter { get; set; } = new(10);

        public delegate Task OnReceiveErrorHandler(NaiveMqClient sender, Exception ex);

        public event OnReceiveErrorHandler OnReceiveErrorAsync;

        public delegate Task OnParseCommandErrorHandler(NaiveMqClient sender, ParseCommandException exception);

        public event OnParseCommandErrorHandler OnParseMessageErrorAsync;

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

        private bool _isStarted;

        private SemaphoreSlim _readSemaphore;
        
        private readonly int _readConcurrency;
        
        private readonly ILogger<NaiveMqClient> _logger;

        private readonly CancellationToken _stoppingToken;

        private readonly SemaphoreSlim _writeSemaphore = new(1, 1);

        private readonly ConcurrentDictionary<Guid, ResponseItem> _responses = new();

        private ICommandConverter _converter = new JsonCommandConverter();

        static NaiveMqClient()
        {
            foreach (var type in typeof(ICommand).Assembly.GetTypes().Where(x => x.GetInterfaces().Any(y => y == typeof(ICommand))))
            {
                _commandTypes.Add(type.Name, type);
            }
        }

        public NaiveMqClient(NaiveMqClientOptions options, ILogger<NaiveMqClient> logger, CancellationToken stoppingToken)
        {
            if (options.TcpClient != null)
            {
                TcpClient = options.TcpClient;
            }
            else
            {
                if (!string.IsNullOrEmpty(options.Host) && options.Port != null)
                {
                    TcpClient = new TcpClient(options.Host, options.Port.Value);
                }
            }

            if (options.ConfirmTimeout != null)
            {
                ConfirmTimeout = options.ConfirmTimeout.Value;
            }

            _readConcurrency = options.ReadConcurrency;
            _logger = logger;
            _stoppingToken = stoppingToken;

            if (TcpClient != null)
            {
                Start();
            }
        }

        public void Start()
        {
            if (!_isStarted)
            {
                _readSemaphore = new(_readConcurrency, _readConcurrency);

                Task.Run(ReceiveAsync);

                _isStarted = true;
            }
        }

        public void Stop()
        {
            if (_isStarted)
            {
                _readSemaphore.Dispose();
                _readSemaphore = null;

                _isStarted = false;
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

            try
            {
                PrepareCommand(request);

                if (request.Confirm)
                {
                    responseItem = new ResponseItem();
                    _responses[request.Id] = responseItem;
                }

                await WriteCommandAsync(request, cancellationToken);

                if (request is Message message && OnSendMessageAsync != null)
                {
                    await OnSendMessageAsync.Invoke(this, message);
                }

                if (request.Confirm)
                {
                    response = await WaitForConfirmation(request, responseItem, cancellationToken);
                }
            }
            catch (OperationCanceledException ex)
            {
                throw new ClientException("Sending request is canceled.", ex);
            }
            finally
            {
                if (request.Confirm)
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

            TcpClient.Dispose();

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

            var entered = await responseItem.SemaphoreSlim.WaitAsync((int)(request.ConfirmTimeout ?? ConfirmTimeout).TotalMilliseconds, cancellationToken);

            if (!entered)
            {
                if (_isStarted)
                {
                    throw new ConfirmationException("Command confirmation expired or canceled.");
                }
                else
                {
                    throw new ClientStoppedException("Sending request is canceled.");
                }
            }
            else
            {
                if (!responseItem.Response.Success)
                {
                    throw new ConfirmationException(responseItem.Response);
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

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($">> {TraceCommand(command)} by {Id}");
                }

                if (OnSendCommandAsync != null)
                {
                    await OnSendCommandAsync.Invoke(this, command);
                }
            }
            catch (ClientException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Stop();
                throw new ClientException("Error writing message to client.", ex);
            }
        }

        private async Task WriteBytesAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            const string stoppedMessage = "Writing message failed because connection is stopped.";

            if (!_isStarted)
            {
                throw new ClientStoppedException(stoppedMessage);
            }

            try
            {
                await _writeSemaphore.WaitAsync(cancellationToken);

                if (!_isStarted)
                {
                    throw new ClientStoppedException(stoppedMessage);
                }

                try
                {
                    var stream = TcpClient.GetStream();

                    await stream.WriteAsync(bytes, cancellationToken);
                }
                catch (Exception ex)
                {
                    throw new ConnectionException("Error writing stream.", ex);
                }
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        private async Task ReceiveAsync()
        {
            while (!_stoppingToken.IsCancellationRequested && _isStarted)
            {
                try
                {
                    var stream = TcpClient.GetStream();

                    var commandNameLength = await ReadLengthAsync(stream);
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
                    var clEx = new ConnectionException("Error reading stream.", ex);
                    await HandleReceiveErrorAsync(clEx);
                    throw clEx;
                }
            };
        }

        private async Task<int> ReadLengthAsync(NetworkStream stream)
        {
            var commandNameLengthBytes = new byte[4];
            await stream.ReadAsync(commandNameLengthBytes, 0, 4, _stoppingToken);
            var commandNameLength = BitConverter.ToInt32(commandNameLengthBytes);
            return commandNameLength;
        }

        private async Task<byte[]> ReadContentAsync(NetworkStream stream, int commandNameLength)
        {
            var commandNameBytes = new byte[commandNameLength];
            await stream.ReadAsync(commandNameBytes, 0, commandNameLength, _stoppingToken);
            return commandNameBytes;
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

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($"<< {TraceCommand(command)} by {Id}");
                }

                await HandleReceiveCommandAsync(command);
            }
            finally
            {
                _readSemaphore.Release();
            }
        }

        private async Task HandleReceiveErrorAsync(Exception ex)
        {
            Stop();

            if (ex is not ClientException)
            {
                _logger.LogError(ex, "Unexpected error occured during handling an incoming command.");
            }

            if (OnReceiveErrorAsync != null)
            {
                await OnReceiveErrorAsync.Invoke(this, ex);
            }
        }

        private async Task HandleReceiveCommandAsync(ICommand command)
        {
            try
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
            catch (ParseCommandException ex)
            {
                if (OnParseMessageErrorAsync != null)
                {
                    await OnParseMessageErrorAsync.Invoke(this, ex);
                }
            }
            catch (Exception ex)
            {
                await HandleReceiveErrorAsync(ex);
                throw;
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
            try
            {
                if (_commandTypes.TryGetValue(commandName, out Type commandType))
                {
                    return ParseCommand(commandBytes, commandType);
                }
                else
                {
                    throw new ParseCommandException(ErrorCode.CommandNotFound, string.Format(ErrorCode.CommandNotFound.GetDescription(), commandName));
                }
            }
            catch (Exception ex)
            {
                throw new ParseCommandException(ErrorCode.UnexpectedErrorDuringMessageParsing, string.Format(ErrorCode.UnexpectedErrorDuringMessageParsing.GetDescription(), ex.GetBaseException().Message));
            }
        }

        private ICommand ParseCommand(byte[] commandBytes, Type commandType)
        {
            ICommand command;

            try
            {
                command = _converter.Deserialize(commandBytes, commandType);
            }
            catch (Exception ex)
            {
                throw new ParseCommandException(ErrorCode.WrongCommandFormat, string.Format(ErrorCode.WrongCommandFormat.GetDescription(), ex.GetBaseException().Message));
            }

            if (command.Id == null || command.Id == Guid.Empty)
            {
                throw new ParseCommandException(ErrorCode.EmptyCommandId, ErrorCode.EmptyCommandId.GetDescription());
            }

            return command;
        }

        private string TraceCommand(ICommand command)
        {
            return $"{command.GetType().Name}: {JsonConvert.SerializeObject(command)}";
        }
    }
}
