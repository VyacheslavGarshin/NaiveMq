using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;
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
                var data = messageEntity.Data;

                if (messageEntity.Persistent == Persistence.DiskOnly)
                {
                    data = await LoadDiskOnlyDataAsync(messageEntity, cancellationToken);
                }

                var message = CreateMessage(messageEntity, data);
                confirmation = await SendMessageAsync(message, cancellationToken);
                messageEntity.Delivered = true;
            }
            catch (Exception ex)
            {
                if (messageEntity.Request)
                {
                    confirmation = FindRequestConfirmation(ex);

                    if (confirmation != null)
                    {
                        messageEntity.Delivered = true;
                    }
                }

                if (confirmation == null)
                {
                    await ReEnqueueMessageAsync(messageEntity);
                }

                if (ex is not ClientException)
                {
                    throw;
                }
            }
            finally
            {
                if (messageEntity.Delivered && messageEntity.Persistent != Persistence.No)
                {
                    await DeleteMessageAssync(messageEntity, cancellationToken);
                }

                if (messageEntity.Request && confirmation != null && confirmation.Data.Length != 0)
                {
                    await SendRequestConfirmationAsync(messageEntity, confirmation, cancellationToken);
                }
            }
        }

        private async Task<ReadOnlyMemory<byte>> LoadDiskOnlyDataAsync(MessageEntity messageEntity, CancellationToken cancellationToken)
        {
            var data = messageEntity.Data;

            if (data.Length == 0)
            {
                var diskEntity = await _context.Storage.PersistentStorage.LoadMessageAsync(_context.User.Entity.Username,
                    messageEntity.Queue, messageEntity.Id, true, cancellationToken);
                data = diskEntity.Data;
            }

            return data;
        }

        private static Confirmation FindRequestConfirmation(Exception ex)
        {
            Confirmation result = null;

            var clientException = ex as ClientException;
            if (clientException != null && clientException.Response != null)
            {
                var response = clientException.Response as Confirmation;
                if (response != null && response.Data.Length != 0)
                {
                    result = response;
                }
            }

            return result;
        }

        private Client.Commands.Message CreateMessage(MessageEntity messageEntity, ReadOnlyMemory<byte> data)
        {
            return new Client.Commands.Message
            {
                Confirm = _confirm,
                ConfirmTimeout = _confirmTimeout,
                Tag = messageEntity.Tag,
                Queue = messageEntity.Queue,
                Request = messageEntity.Request,
                Persistent = messageEntity.Persistent,
                RoutingKey = messageEntity.RoutingKey,
                Data = data
            };
        }

        private async Task DeleteMessageAssync(MessageEntity messageEntity, CancellationToken cancellationToken)
        {
            await _context.Storage.PersistentStorage.DeleteMessageAsync(_context.User.Entity.Username,
                _queue.Entity.Name, messageEntity.Id, cancellationToken);
        }

        private async Task ReEnqueueMessageAsync(MessageEntity messageEntity)
        {
            using var handler = new MessageHandler();
            await handler.ExecuteEntityAsync(_context, messageEntity);
        }

        private async Task<Confirmation> SendMessageAsync(Client.Commands.Message message, CancellationToken cancellationToken)
        {
            var result = await _context.Client.SendAsync(message, true, cancellationToken);
            return result;
        }

        private async Task SendRequestConfirmationAsync(MessageEntity messageEntity, Confirmation result, CancellationToken cancellationToken)
        {
            if (messageEntity.ClientId != null && _context.Storage.TryGetClient(messageEntity.ClientId.Value, out var receiverContext))
            {
                var confirmation = new Confirmation
                {
                    RequestId = messageEntity.Id,
                    RequestTag = messageEntity.Tag,
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