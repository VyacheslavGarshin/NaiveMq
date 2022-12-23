using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Dto;
using NaiveMq.Service.Entities;
using NaiveMq.Service.Enums;
using NaiveMq.Service.Handlers;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace NaiveMq.Service.Cogs
{
    public class SubscriptionCog : IDisposable
    {
        private static TimerService _timerService = new(TimeSpan.FromSeconds(1));

        public TimeSpan? IdleTime { get; private set; }

        private DateTime? _lastSendDate;

        private DateTime? _lastRedirectSendDate;
        
        private DateTime? _lastHintSendDate;

        private bool _proxyStarted;

        private readonly QueueCog _queue;

        private readonly bool _confirm;

        private readonly TimeSpan? _confirmTimeout;

        private readonly ClusterStrategy _clusterStrategy;

        private readonly TimeSpan _clusterIdleTimout;

        private readonly ClientContext _context;
        
        public bool Started { get; private set; }

        private CancellationTokenSource _cancellationTokenSource;

        public SubscriptionCog(ClientContext context, QueueCog queue, bool confirm, TimeSpan? confirmTimeout, ClusterStrategy clusterStrategy, TimeSpan clusterIdleTimout)
        {
            _context = context;
            _queue = queue;
            _confirm = confirm;
            _confirmTimeout = confirmTimeout;
            _clusterStrategy = clusterStrategy;
            _clusterIdleTimout = clusterIdleTimout;
        }

        public void Start()
        {
            if (!Started)
            {
                Stop();

                _cancellationTokenSource = new CancellationTokenSource();
                _timerService.Add(this, OnTimer);

                Task.Run(() => SendAsync(_cancellationTokenSource.Token), CancellationToken.None); // do not end this task of messages will be lost

                Started = true;
            }
        }

        public void Stop()
        {
            if (Started)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _timerService.Remove(this);
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

                while (Started && !cancellationToken.IsCancellationRequested)
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
                        _lastSendDate = DateTime.UtcNow;

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

                var message = CreateMessage(messageEntity, data);
                response = await SendMessageAsync(message, cancellationToken);

                messageEntity.Delivered = true;
            }
            catch (Exception ex)
            {
                response = FindRequestResponse(messageEntity, ex);

                if (ex is not ClientException)
                {
                    throw;
                }
            }
            finally
            {
                if (messageEntity.Delivered)
                {
                    if (messageEntity.Persistent != Persistence.No)
                    {
                        await DeleteMessageAssync(messageEntity);
                    }
                }
                else
                {
                    await ReEnqueueMessageAsync(messageEntity);
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
                var diskEntity = await _context.Storage.PersistentStorage.LoadMessageAsync(_queue.Entity.User,
                    _queue.Entity.Name, messageEntity.Id, true, cancellationToken);
                data = diskEntity.Data;
            }

            return data;
        }

        private static MessageResponse FindRequestResponse(MessageEntity messageEntity, Exception ex)
        {
            MessageResponse result = null;

            if (messageEntity.Request)
            {
                if (ex is ClientException clientException && clientException.Response != null)
                {
                    if (clientException.Response is MessageResponse response && response.Response)
                    {
                        result = response;
                    }
                }

                if (result != null)
                {
                    messageEntity.Delivered = true;
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

        private async Task DeleteMessageAssync(MessageEntity messageEntity)
        {
            await _context.Storage.PersistentStorage.DeleteMessageAsync(_queue.Entity.User,
                _queue.Entity.Name, messageEntity.Id, CancellationToken.None); // none cancellation to ensure operation is not interrupted
        }

        private async Task ReEnqueueMessageAsync(MessageEntity messageEntity)
        {
            var handler = new MessageHandler();
            await handler.ExecuteEntityAsync(_context, messageEntity, null, CancellationToken.None); // none cancellation to ensure operation is not interrupted
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

        private void OnTimer()
        {
            if (_lastSendDate != null)
            {
                IdleTime = DateTime.UtcNow.Subtract(_lastSendDate.Value);
            }

            if (_queue.Length == 0 && _context.Storage.Cluster.Started 
                && (IdleTime == null || IdleTime > _clusterIdleTimout))
            {
                switch (_clusterStrategy)
                {
                    case ClusterStrategy.Proxy:
                        if (!_proxyStarted)
                        {
                            Task.Run(StartProxyAsync);
                            _proxyStarted = true;
                        }
                        break;
                    case ClusterStrategy.Redirect:
                        if (_lastRedirectSendDate == null || DateTime.UtcNow.Subtract(_lastRedirectSendDate.Value) > _clusterIdleTimout)
                        {
                            Task.Run(SendRedirectCommandAsync);
                            _lastRedirectSendDate = DateTime.UtcNow;
                        }
                        break;
                    case ClusterStrategy.Hint:
                        if (_lastHintSendDate == null || DateTime.UtcNow.Subtract(_lastHintSendDate.Value) > _clusterIdleTimout)
                        {
                            Task.Run(SendHintCommandAsync);
                            _lastHintSendDate = DateTime.UtcNow;
                        }
                        break;
                    case ClusterStrategy.Wait:
                        break;
                }
            }
        }

        private Task StartProxyAsync()
        {
            throw new NotImplementedException();
        }

        private async Task SendRedirectCommandAsync()
        {
            var hints = GetQueueHints().Where(x => x.Subscriptions < _queue.Counters.Subscriptions.Value);

            if (hints.Any())
            {
                var hint = hints.OrderBy(x => x.Subscriptions).ThenByDescending(x => x.Length).First();
                await _context.Client.SendAsync(new ClusterRedirect(hint.Host));
            }
        }

        private async Task SendHintCommandAsync()
        {
            var hints = GetQueueHints().ToList();

            if (hints.Any())
            {
                await _context.Client.SendAsync(new ClusterHint(hints));
            }
        }

        private IEnumerable<QueueHint> GetQueueHints()
        {
            var hints = new List<QueueHint>();

            foreach (var server in _context.Storage.Cluster.Servers.Values)
            {
                if (server.Name != _context.Storage.Cluster.Self.Name
                    && server.ActiveQueues.TryGetValue(ActiveQueue.CreateKey(_queue.Entity.User, _queue.Entity.Name), out var activeQueue))
                {
                    yield return new QueueHint
                    {
                        Name = _queue.Entity.Name,
                        Host = server.Host.ToString(),
                        Length = activeQueue.Length,
                        Subscriptions = activeQueue.Subscriptions,
                    };
                }
            }
        }
    }
}