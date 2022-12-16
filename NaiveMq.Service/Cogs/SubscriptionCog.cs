using Microsoft.Extensions.Logging;
using NaiveMq.Client;
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
                    try
                    {
                        var messageEntity = await _queue.TryDequeueAsync(cancellationToken);

                        if (messageEntity != null)
                        {
                            await ProcessMessageAsync(messageEntity, cancellationToken);
                        }
                    }
                    catch (ServerException ex)
                    {
                        if (ex.ErrorCode == ErrorCode.QueueStopped)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }
                        else
                        {
                            throw;
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
                if (_context.Client.Started)
                {
                    _context.Logger.LogError(ex, "Unexpected error during sending messages from subscription.");
                }

                throw;
            }
        }

        private async Task ProcessMessageAsync(MessageEntity messageEntity, CancellationToken cancellationToken)
        {
            Confirmation confirmation = null;

            try
            {
                if (messageEntity.Persistent == Persistence.DiskOnly)
                {
                    var diskMessageEntity = await _context.Storage.PersistentStorage.LoadMessageAsync(_context.User.Entity.Username,
                        messageEntity.Queue, messageEntity.Id, true, cancellationToken);

                    messageEntity.Data = diskMessageEntity.Data;
                }

                confirmation = await SendMessageAsync(messageEntity, cancellationToken);

                if (messageEntity.Persistent != Persistence.No)
                {
                    await _context.Storage.PersistentStorage.DeleteMessageAsync(_context.User.Entity.Username,
                        _queue.Entity.Name, messageEntity.Id, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                if (ex is ClientException clientException && clientException.Response != null)
                {
                    confirmation = clientException.Response as Confirmation;
                }

                if (!messageEntity.Request)
                {
                    await ReEnqueueMessageAsync(messageEntity);
                }
            }
            finally
            {
                if (confirmation != null && messageEntity.Request)
                {
                    await SendConfirmationAsync(messageEntity, confirmation, cancellationToken);
                }
            }
        }

        private async Task ReEnqueueMessageAsync(MessageEntity messageEntity)
        {
            using var handler = new MessageHandler();
            await handler.ExecuteEntityAsync(_context, messageEntity);
        }

        private async Task<Confirmation> SendMessageAsync(MessageEntity messageEntity, CancellationToken cancellationToken)
        {
            var message = new Message
            {
                Confirm = _confirm,
                ConfirmTimeout = _confirmTimeout,
                Tag = messageEntity.Tag,
                Queue = messageEntity.Queue,
                Request = messageEntity.Request,
                Persistent = messageEntity.Persistent,
                RoutingKey = messageEntity.RoutingKey,
                Data = messageEntity.Data
            };

            var result = await _context.Client.SendAsync(message, true, cancellationToken);

            if (result != null && result.Data.Length != 0)
            {
                result.RequestId = messageEntity.Id;
                result.RequestTag = messageEntity.Tag;
            }

            return result;
        }

        private async Task SendConfirmationAsync(MessageEntity messageEntity, Confirmation result, CancellationToken cancellationToken)
        {
            if (messageEntity.ClientId != null && _context.Storage.TryGetClient(messageEntity.ClientId.Value, out var receiverContext))
            {
                var confirmation = new Confirmation
                {
                    RequestId = result.RequestId,
                    RequestTag = result.RequestTag,
                    Success = result.Success,
                    ErrorCode = result.ErrorCode,
                    ErrorMessage = result.ErrorMessage,
                    Data = result.Data
                };

                await receiverContext.Client.SendAsync(confirmation, cancellationToken);
            }
        }
    }
}