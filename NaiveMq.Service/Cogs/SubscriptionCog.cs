using Microsoft.Extensions.Logging;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Entities;
using NaiveMq.Service.Handlers;

namespace NaiveMq.Service.Cogs
{
    public class SubscriptionCog : IDisposable
    {
        public bool _confirm { get; set; }

        public TimeSpan? _confirmTimeout { get; set; }

        private readonly ClientContext _context;

        private readonly QueueCog _queue;

        private bool _isStarted;

        private CancellationTokenSource _cancellationTokenSource;

        private Task _sendTask;

        public SubscriptionCog(ClientContext context, QueueCog queue, bool confirm, TimeSpan? confirmTimeout)
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
                while (_isStarted && !_context.StoppingToken.IsCancellationRequested)
                {
                    var messageEntity = await _queue.TryDequeue(cancellationToken);

                    if (messageEntity != null)
                    {
                        try
                        {
                            if (messageEntity.Persistent == Persistent.DiskOnly)
                            {
                                var diskMessageEntity = await _context.Storage.PersistentStorage.LoadMessageAsync(_context.User.Username,
                                    messageEntity.Queue, messageEntity.Id, cancellationToken);
                                
                                messageEntity.Data = diskMessageEntity.Data;
                            }

                            var result = await SendMessage(messageEntity, cancellationToken);

                            if (messageEntity.Persistent != Persistent.No)
                            {
                                await _context.Storage.PersistentStorage.DeleteMessageAsync(_context.User.Username, 
                                    _queue.Entity.Name, messageEntity.Id, cancellationToken);
                            }

                            if (messageEntity.Request)
                            {
                                await SendConfirmation(messageEntity, result, cancellationToken);
                            }
                        }
                        catch (Exception)
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
                if (!_context.Client.Started)
                {
                    _context.Logger.LogError(ex, "Unexpected error during sending messages from subscription.");
                }

                throw;
            }
        }

        private async Task ReEnqueueMessage(MessageEntity messageEntity)
        {
            using var handler = new MessageHandler();
            await handler.ExecuteEntityAsync(_context, messageEntity);
        }

        private async Task SendConfirmation(MessageEntity messageEntity, Confirmation result, CancellationToken cancellationToken)
        {
            if (messageEntity.ClientId != null && _context.Storage.TryGetClient(messageEntity.ClientId.Value, out var receiverContext))
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
                Persistent = messageEntity.Persistent,
                RoutingKey = messageEntity.RoutingKey,
                Data = messageEntity.Data
            };

            return await _context.Client.SendAsync(message, cancellationToken);
        }
    }
}