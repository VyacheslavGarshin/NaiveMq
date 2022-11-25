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

        public string User { get; protected set; } = "guest";

        public delegate Task OnReceiveErrorHandler(NaiveMqClient sender, Exception ex);

        public event OnReceiveErrorHandler OnReceiveErrorAsync;

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
        }

        public NaiveMqClient(string host, int port, ILogger<NaiveMqClient> logger, CancellationToken stoppingToken) : this(logger, stoppingToken)
        {
            TcpClient = new TcpClient(host, port);
        }

        public void Start()
        {
            if (!_isStarted)
            {
                DisposeStreams();

                _stream = TcpClient.GetStream();
                _streamReader = new StreamReader(_stream);
                _streamWriter = new StreamWriter(_stream);

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
        /// 
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
                    _responses[request.Id.Value] = responseItem;
                }

                await SendAsync(commandText, cancellationToken);

                if (request.Confirm)
                {
                    var entered = await responseItem.SemaphoreSlim.WaitAsync((int)(request.ConfirmTimeout ?? Timeout).TotalMilliseconds, cancellationToken);

                    if (!entered)
                    {
                        throw new TimeoutException("Command confirmation expired or canceled.");
                    }
                    else
                    {
                        if (!responseItem.Response.IsSuccess)
                        {
                            throw new ConfirmationException(responseItem.Response);
                        }
                        else
                        {
                            response = responseItem.Response;
                        }
                    }
                }
            }
            finally
            {
                if (request.Confirm)
                {
                    _responses.TryRemove(request.Id.Value, out var _);
                    responseItem.Dispose();
                }
            }

            return (TResponse)response;
        }

        public async Task SendAsync(IResponse response, CancellationToken cancellationToken)
        {
            var text = CreateMessage(response);
            await SendAsync(text, cancellationToken);
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

        public void Dispose()
        {
            Stop();

            TcpClient.Dispose();

            _writeSemaphore.Dispose();

            ReadCounter.Dispose();
            WriteCounter.Dispose();
        }

        private static ICommand ParseMessage(string message)
        {
            try
            {
                var split = (message ?? string.Empty).Split("|", 3);

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
            const string message = "Writing message failed because connection is stopped.";

            if (!_isStarted)
            {
                throw new ClientStoppedException(message);
            }

            await _writeSemaphore.WaitAsync(cancellationToken);

            try
            {
                if (!_isStarted)
                {
                    throw new ClientStoppedException(message);
                }

                try
                {
                    await _streamWriter.WriteLineAsync(text.ToCharArray(), cancellationToken);
                    await _streamWriter.FlushAsync();

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
                catch (Exception ex)
                {
                    Stop();
                    throw new ConnectionException("Error writing message to client.", ex);
                }
            }
            finally
            {
                if (_isStarted)
                {
                    _writeSemaphore.Release();
                }
            }
        }

        private async Task ReceiveAsync()
        {
            while (!_stoppingToken.IsCancellationRequested && _isStarted)
            {
                string message;

                try
                {
                    message = await _streamReader.ReadLineAsync();
                }
                catch (Exception ex)
                {
                    await HandleReceiveErrorAsync(ex);
                    return;
                }

                ReadCounter.Add();

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($"<{Id}:{message}");
                }

                if (message == null)
                {
                    await HandleReceiveErrorAsync(new Exception("Incoming message is null."));
                    return;
                }
                else
                {
                    if (OnReceiveAsync != null)
                    {
                        await OnReceiveAsync.Invoke(this, message);
                    }

                    await HandleReceiveCommandAsync(message);
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

        private async Task HandleReceiveCommandAsync(string message)
        {
            try
            {
                var command = ParseMessage(message);

                if (OnReceiveCommandAsync != null)
                {
                    await OnReceiveCommandAsync.Invoke(this, command);
                }

                if (command is IRequest && OnReceiveRequestAsync != null)
                {
                    await OnReceiveRequestAsync.Invoke(this, (IRequest)command);
                }

                if (command is IResponse)
                {
                    HandleResponse((IResponse)command);

                    if (OnReceiveResponseAsync != null)
                    {
                        await OnReceiveResponseAsync.Invoke(this, (IResponse)command);
                    }
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
                Stop();

                throw;
            }
        }

        private void HandleResponse(IResponse response)
        {
            if (response != null)
            {
                if (_responses.TryGetValue(response.RequestId.Value, out var responseItem))
                {
                    responseItem.Response = response;
                    responseItem.SemaphoreSlim.Release();
                }
            }
        }
    }
}
