using Microsoft.Extensions.Logging;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Entities;
using NaiveMq.Client.Exceptions;
using NaiveMq.Service.Handlers;

namespace NaiveMq.Service.Cogs
{
    public class Subscription : IDisposable
    {
        public bool _confirm { get; set; }

        public TimeSpan? _confirmTimeout { get; set; }

        private readonly ClientContext _context;

        private readonly Queue _queue;

        private bool _isStarted;

        private CancellationTokenSource _cancellationTokenSource;

        private Task _sendTask;

        public Subscription(ClientContext context, Queue queue, bool confirm, TimeSpan? confirmTimeout)
        {
            _context = context;
            _queue = queue;
            _confirm = confirm;
            _confirmTimeout = confirmTimeout;
        }

        public void Start()
        {
            if (!_isStarted)
            {
                Stop();

                _cancellationTokenSource = new CancellationTokenSource();
                _sendTask = Task.Run(SendAsync, _cancellationTokenSource.Token);

                _isStarted = true;
            }
        }

        public void Stop()
        {
            if (_isStarted)
            {
                _cancellationTokenSource.Cancel();

                _isStarted = false;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task SendAsync()
        {
            var cancellationToken = _cancellationTokenSource.Token;

            try
            {
                while (_isStarted && !_context.CancellationToken.IsCancellationRequested)
                {
                    await _queue.WaitDequeueAsync(cancellationToken);

                    if (_queue.TryDequeue(out var messageEntity))
                    {
                        try
                        {
                            var result = await SendMessage(messageEntity, cancellationToken);

                            if (messageEntity.Durable)
                            {
                                await _context.Storage.PersistentStorage.DeleteMessageAsync(_context.User.Username, _queue.Name, messageEntity.Id, cancellationToken);
                            }

                            if (messageEntity.Request)
                            {
                                await SendConfirmation(messageEntity, result, cancellationToken);
                            }
                        }
                        catch (ClientException)
                        {
                            if (!messageEntity.Request)
                            {
                                await ReEnqueueMessage(messageEntity);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // it's ok to exit this way
            }
            catch (Exception ex)
            {
                _context.Logger.LogError(ex, "Unexpected error during sending messages from subscription.");
                throw;
            }
        }

        private async Task ReEnqueueMessage(MessageEntity messageEntity)
        {
            var messageCommand = new Message
            {
                Id = messageEntity.Id,
                Queue = messageEntity.Queue,
                Request = messageEntity.Request,
                Durable = messageEntity.Durable,
                BindingKey = messageEntity.BindingKey,
                Text = messageEntity.Text
            };

            await new MessageHandler().ExecuteAsync(_context, messageCommand);
        }

        private async Task SendConfirmation(MessageEntity messageEntity, Confirmation result, CancellationToken cancellationToken)
        {
            if (_context.Storage.TryGetClient(messageEntity.ClientId, out var receiverContext))
            {
                var confirmation = new Confirmation
                {
                    RequestId = result.RequestId,
                    Success = result.Success,
                    ErrorCode = result.ErrorCode,
                    ErrorMessage = result.ErrorMessage,
                    Text = result.Text
                };

                await receiverContext.Client.SendAsync(confirmation, cancellationToken);
            }
        }

        private async Task<Confirmation> SendMessage(MessageEntity messageEntity, CancellationToken cancellationToken)
        {
            var message = new Message
            {
                Id = messageEntity.Id,
                Confirm = _confirm,
                ConfirmTimeout = _confirmTimeout,
                Queue = messageEntity.Queue,
                Request = messageEntity.Request,
                Durable = messageEntity.Durable,
                BindingKey = messageEntity.BindingKey,
                Text = messageEntity.Text
            };

            return await _context.Client.SendAsync(message, cancellationToken);
        }
    }
}