using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Entities;
using NaiveMq.Service.Enums;
using NaiveMq.Service.Handlers;

namespace NaiveMq.Service.Cogs
{
    public class SubscriptionCog : IDisposable
    {
        private readonly QueueCog _queue;

        private bool _confirm { get; set; }

        private TimeSpan? _confirmTimeout { get; set; }

        private readonly ClusterStrategy _clusterStrategy;
        
        private readonly ClientContext _context;
        
        private bool _isStarted;

        private CancellationTokenSource _cancellationTokenSource;

        private Task _sendTask;
        
        public SubscriptionCog(ClientContext context, QueueCog queue, bool confirm, TimeSpan? confirmTimeout, ClusterStrategy clusterStrategy)
        {
            _context = context;
            _queue = queue;
            _confirm = confirm;
            _confirmTimeout = confirmTimeout;
            _clusterStrategy = clusterStrategy;
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
                        if (_queue.Status != QueueStatus.Started)
                        {
                            continue;
                        }

                        var messageEntity = await _queue.TryDequeueAsync(cancellationToken);

                        if (messageEntity != null)
                        {
                            await ProcessMessageAsync(messageEntity, cancellationToken);
                        }                   
                    }
                    catch (ServerException ex)
                    {
                        if (ex.ErrorCode != ErrorCode.QueueStopped)                        
                        {
                            throw;
                        }
                    }
                    finally
                    {
                        if (_queue.Status != QueueStatus.Deleted)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
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
            MessageResponse response = null;

            try
            {
                var data = messageEntity.Data;

                if (messageEntity.Persistent == Persistence.DiskOnly)
                {
                    data = await LoadDiskOnlyDataAsync(messageEntity, cancellationToken);
                }

                var message = CreateMessage(messageEntity, data);
                response = await SendMessageAsync(message, cancellationToken);
                messageEntity.Delivered = true;
            }
            catch (Exception ex)
            {
                if (messageEntity.Request)
                {
                    response = FindRequestResponse(ex);

                    if (response != null)
                    {
                        messageEntity.Delivered = true;
                    }
                }

                if (response == null)
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

                if (messageEntity.Request && response != null && response.Response)
                {
                    await SendRequestResponseAsync(messageEntity, response, cancellationToken);
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

        private static MessageResponse FindRequestResponse(Exception ex)
        {
            MessageResponse result = null;

            var clientException = ex as ClientException;
            if (clientException != null && clientException.Response != null)
            {
                var response = clientException.Response as MessageResponse;
                if (response != null && response.Response)
                {
                    result = response;
                }
            }

            return result;
        }

        private Message CreateMessage(MessageEntity messageEntity, ReadOnlyMemory<byte> data)
        {
            var result = messageEntity.ToCommand();

            result.Confirm = _confirm;
            result.ConfirmTimeout = _confirmTimeout;
            result.Data = data;

            return result;
        }

        private async Task DeleteMessageAssync(MessageEntity messageEntity, CancellationToken cancellationToken)
        {
            await _context.Storage.PersistentStorage.DeleteMessageAsync(_context.User.Entity.Username,
                _queue.Entity.Name, messageEntity.Id, cancellationToken);
        }

        private async Task ReEnqueueMessageAsync(MessageEntity messageEntity)
        {
            var handler = new MessageHandler();
            await handler.ExecuteEntityAsync(_context, messageEntity);
        }

        private async Task<MessageResponse> SendMessageAsync(Message message, CancellationToken cancellationToken)
        {
            var result = await _context.Client.SendAsync(message, true, cancellationToken);
            return result;
        }

        private async Task SendRequestResponseAsync(MessageEntity messageEntity, MessageResponse result, CancellationToken cancellationToken)
        {
            if (messageEntity.ClientId != null && _context.Storage.TryGetClientContext(messageEntity.ClientId.Value, out var receiverContext))
            {
                var response = result.Copy();

                response.RequestId = messageEntity.Id;
                response.RequestTag = messageEntity.Tag;

                await receiverContext.Client.SendAsync(response, cancellationToken);
            }
        }
    }
}