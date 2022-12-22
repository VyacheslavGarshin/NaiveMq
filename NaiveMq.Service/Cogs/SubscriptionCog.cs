using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Entities;
using NaiveMq.Service.Enums;
using NaiveMq.Service.Handlers;
using System.Diagnostics;

namespace NaiveMq.Service.Cogs
{
    public class SubscriptionCog : IDisposable
    {
        private readonly QueueCog _queue;

        private readonly bool _confirm;

        private readonly TimeSpan? _confirmTimeout;

        private readonly ClusterStrategy _clusterStrategy;
        
        private readonly ClientContext _context;
        
        public bool Started { get; private set; }

        private CancellationTokenSource _cancellationTokenSource;

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
            if (!Started)
            {
                Stop();

                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => SendAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                Started = true;
            }
        }

        public void Stop()
        {
            if (Started)
            {
                _cancellationTokenSource.Cancel();
                Started = false;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task SendAsync(CancellationToken cancellationToken)
        {
            try
            {
                _queue.Counters.Subscriptions.Add();

                while (Started && !_context.StoppingToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (_queue.Status != QueueStatus.Started)
                        {
                            if (_queue.Status == QueueStatus.Deleted)
                            {
                                break;
                            }
                            else
                            {
                                continue;
                            }
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
                        if (_queue.Status != QueueStatus.Started)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
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
            finally
            {
                _queue.Counters.Subscriptions.Add(-1);

                if (_context.Subscriptions.TryRemove(_queue, out var subscription))
                {
                    subscription.Dispose();
                }
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

                UpdateAvgLifeTime(messageEntity);

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

                _queue.Counters.AvgIoTime.Add(messageEntity.IoTime.Value);

                if (messageEntity.Request && response != null && response.Response)
                {
                    await SendRequestResponseAsync(messageEntity, response, cancellationToken);
                }
            }
        }

        private void UpdateAvgLifeTime(MessageEntity messageEntity)
        {
            var diff = DateTime.UtcNow.Subtract(messageEntity.Date);
            _queue.Counters.AvgLifeTime.Add((long)diff.TotalMilliseconds);
        }

        private async Task<ReadOnlyMemory<byte>> LoadDiskOnlyDataAsync(MessageEntity messageEntity, CancellationToken cancellationToken)
        {
            var data = messageEntity.Data;

            if (data.Length == 0)
            {
                var sw = new Stopwatch();
                sw.Start();

                var diskEntity = await _context.Storage.PersistentStorage.LoadMessageAsync(_queue.Entity.User,
                    _queue.Entity.Name, messageEntity.Id, true, cancellationToken);

                sw.Stop();
                messageEntity.IoTime.Add(sw.ElapsedMilliseconds);

                data = diskEntity.Data;
            }

            return data;
        }

        private static MessageResponse FindRequestResponse(Exception ex)
        {
            MessageResponse result = null;

            if (ex is ClientException clientException && clientException.Response != null)
            {
                if (clientException.Response is MessageResponse response && response.Response)
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
            var sw = new Stopwatch();
            sw.Start();

            await _context.Storage.PersistentStorage.DeleteMessageAsync(_queue.Entity.User,
                _queue.Entity.Name, messageEntity.Id, cancellationToken);

            sw.Stop();
            messageEntity.IoTime.Add(sw.ElapsedMilliseconds);
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