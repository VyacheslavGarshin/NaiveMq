using Microsoft.Extensions.Logging;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Exceptions;
using Newtonsoft.Json;
using System;
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
            public SemaphoreSlim SemaphoreSlim { get; set; } = new SemaphoreSlim(0, 1);

            public IResponse Response { get; set; }

            public void Dispose()
            {
                SemaphoreSlim.Dispose();
            }
        }

        public int Id => GetHashCode();

        public TcpClient TcpClient { get; set; }

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

        public SpeedCounter WriteCounter { get; set; } = new SpeedCounter(10);

        public SpeedCounter ReadCounter { get; set; } = new SpeedCounter(10);

        public delegate Task OnReceiveErrorHandler(NaiveMqClient sender, Exception ex);

        public event OnReceiveErrorHandler OnReceiveErrorAsync;

        public delegate Task OnReceiveMessageHandler(NaiveMqClient sender, Message command);

        public event OnReceiveMessageHandler OnReceiveMessageAsync;

        public delegate Task OnReceiveCommandHandler(NaiveMqClient sender, ICommand command);

        public event OnReceiveCommandHandler OnReceiveCommandAsync;

        public delegate Task OnReceiveRequestHandler(NaiveMqClient sender, IRequest command);

        public event OnReceiveRequestHandler OnReceiveRequestAsync;

        public delegate Task OnReceiveResponseHandler(NaiveMqClient sender, IResponse command);

        public event OnReceiveResponseHandler OnReceiveResponseAsync;

        public delegate Task OnParseCommandErrorHandler(NaiveMqClient sender, ParseCommandException exception);

        public event OnParseCommandErrorHandler OnParseMessageErrorAsync;

        public delegate Task OnReceiveHandler(NaiveMqClient sender, string message);

        public event OnReceiveHandler OnReceiveAsync;

        public delegate Task OnSendHandler(NaiveMqClient sender, string message);

        public event OnSendHandler OnSendAsync;

        public delegate Task OnSendMessageHandler(NaiveMqClient sender, Message command);

        public event OnSendMessageHandler OnSendMessageAsync;

        private static readonly Dictionary<string, Type> _commandTypes = new Dictionary<string, Type>();

        private NetworkStream _stream;

        private StreamReader _streamReader;

        private StreamWriter _streamWriter;

        private bool _isStarted;

        private readonly ILogger<NaiveMqClient> _logger;

        private readonly CancellationToken _stoppingToken;

        private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);

        private readonly ConcurrentDictionary<Guid, ResponseItem> _responses = new ConcurrentDictionary<Guid, ResponseItem>();

        static NaiveMqClient()
        {
            foreach (var type in typeof(ICommand).Assembly.GetTypes().Where(x => x.GetInterfaces().Any(y => y == typeof(ICommand))))
            {
                _commandTypes.Add(type.Name, type);
            }
        }

        public NaiveMqClient()
        {

        }

        public NaiveMqClient(ILogger<NaiveMqClient> logger, CancellationToken stoppingToken)
        {
            _logger = logger;
            _stoppingToken = stoppingToken;
        }

        public NaiveMqClient(TcpClient tcpClient, ILogger<NaiveMqClient> logger, CancellationToken stoppingToken) : this(logger, stoppingToken)
        {
            TcpClient = tcpClient;
            Start();
        }

        public NaiveMqClient(string host, int port, ILogger<NaiveMqClient> logger, CancellationToken stoppingToken) : this(logger, stoppingToken)
        {
            TcpClient = new TcpClient(host, port);
            Start();
        }

        public void Start()
        {
            if (!_isStarted)
            {
                DisposeStreams();

                _stream = TcpClient.GetStream();
                _streamReader = new StreamReader(_stream, Encoding.UTF8);
                _streamWriter = new StreamWriter(_stream, Encoding.UTF8);

                Task.Run(async () => await ReceiveAsync());

                _isStarted = true;
            }
        }

        public void Stop()
        {
            if (_isStarted)
            {
                DisposeStreams();

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
                var commandText = CreateMessage(request);

                if (request.Confirm)
                {
                    responseItem = new ResponseItem();
                    _responses[request.Id] = responseItem;
                }

                await SendAsync(commandText, cancellationToken);

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
            var text = CreateMessage(response);
            await SendAsync(text, cancellationToken);
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

        private async Task<IResponse> WaitForConfirmation<TResponse>(IRequest<TResponse> request, ResponseItem responseItem, CancellationToken cancellationToken)
            where TResponse : IResponse
        {
            IResponse response;

            var entered = await responseItem.SemaphoreSlim.WaitAsync((int)(request.ConfirmTimeout ?? Timeout).TotalMilliseconds, cancellationToken);

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

        private void DisposeStreams()
        {
            if (_streamReader != null)
            {
                try
                {
                    // somehow NetworkStream it's already disposed
                    _streamReader.Dispose();
                }
                catch (ObjectDisposedException)
                {

                }

                _streamReader = null;
            }

            if (_streamWriter != null)
            {
                try
                {
                    // somehow NetworkStream it's already disposed
                    _streamWriter.Dispose();
                }
                catch (ObjectDisposedException)
                {

                }

                _streamWriter = null;
            }

            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        private static ICommand ParseMessage(string message)
        {
            try
            {
                var split = (message ?? string.Empty).Split("|", 2);

                if (split.Length < 2)
                {
                    throw new ParseCommandException(ErrorCode.WrongMessageFormat, ErrorCode.WrongMessageFormat.GetDescription());
                }

                var commandName = split[0];
                var commandJson = split[1];

                if (_commandTypes.TryGetValue(commandName, out Type commandType))
                {
                    return ParseCommand(commandJson, commandType);
                }
                else
                {
                    throw new ParseCommandException(ErrorCode.WrongMessageFormat, string.Format(ErrorCode.CommandNotFound.GetDescription(), commandName));
                }
            }
            catch (Exception ex)
            {
                throw new ParseCommandException(ErrorCode.UnexpectedErrorDuringMessageParsing, string.Format(ErrorCode.UnexpectedErrorDuringMessageParsing.GetDescription(), ex.GetBaseException().Message));
            }
        }

        private static ICommand ParseCommand(string commandJson, Type commandType)
        {
            ICommand command;

            try
            {
                command = (ICommand)JsonConvert.DeserializeObject(commandJson, commandType);
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

        private string CreateMessage(ICommand command)
        {
            if (command.Id == null || command.Id == Guid.Empty)
                command.Id = Guid.NewGuid();

            return $"{command.GetType().Name}|{JsonConvert.SerializeObject(command)}";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ConnectionException"></exception>
        /// <exception cref="ClientStoppedException"></exception>
        private async Task SendAsync(string text, CancellationToken cancellationToken)
        {
            try
            {
                var charArray = text.ToCharArray();
                
                await WriteLineAsync(charArray, cancellationToken);

                WriteCounter.Add();

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($">{Id}:{text}");
                }

                if (OnSendAsync != null)
                {
                    await OnSendAsync.Invoke(this, text);
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

        private async Task WriteLineAsync(char[] charArray, CancellationToken cancellationToken)
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
                    await _streamWriter.WriteLineAsync(charArray, cancellationToken);
                    await _streamWriter.FlushAsync();
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
                string text;

                try
                {
                    text = await _streamReader.ReadLineAsync();
                }
                catch (Exception ex)
                {
                    var clEx = new ConnectionException("Error reading stream.", ex);
                    await HandleReceiveErrorAsync(clEx);
                    throw clEx;
                }

                ReadCounter.Add();

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($"<{Id}:{text}");
                }

                if (text == null)
                {
                    var ex = new ConnectionException("Incoming message is null.", null);
                    await HandleReceiveErrorAsync(ex);
                    throw ex;
                }
                else
                {
                    try
                    {
                        if (OnReceiveAsync != null)
                        {
                            await OnReceiveAsync.Invoke(this, text);
                        }

                        await HandleReceiveCommandAsync(text);
                    }
                    catch (Exception ex)
                    {
                        await HandleReceiveErrorAsync(ex);
                        throw;
                    }
                }
            };
        }

        private async Task HandleReceiveErrorAsync(Exception ex)
        {
            Stop();

            if (OnReceiveErrorAsync != null)
            {
                await OnReceiveErrorAsync.Invoke(this, ex);
            }
        }

        private async Task HandleReceiveCommandAsync(string text)
        {
            try
            {
                var command = ParseMessage(text);

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
            catch
            {
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
    }
}
