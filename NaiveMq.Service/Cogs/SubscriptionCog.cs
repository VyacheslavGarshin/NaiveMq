using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Dto;
using NaiveMq.Service.Entities;

namespace NaiveMq.Service.Cogs
{
    public class SubscriptionCog : IDisposable
    {
        public TimeSpan? IdleTime { get; private set; }

        private static readonly TimerService _timerService = new(TimeSpan.FromSeconds(1));

        private DateTime? _lastSendDate;

        private DateTime? _lastRedirectSendDate;
        
        private DateTime? _lastHintSendDate;

        private bool _proxyStarted;

        private QueueCog _queue;

        private readonly bool _confirm;

        private readonly TimeSpan? _confirmTimeout;

        private readonly ClusterStrategy _clusterStrategy;

        private readonly TimeSpan _clusterIdleTimout;

        private readonly NaiveMqClientWithContext _client;

        private CancellationTokenSource _cancellationTokenSource;

        private Task _task;

        private NaiveMqClient _proxyClient;

        public SubscriptionCog(NaiveMqClientWithContext client, QueueCog queue, bool confirm, TimeSpan? confirmTimeout, ClusterStrategy clusterStrategy, TimeSpan clusterIdleTimout)
        {
            _client = client;
            _queue = queue;
            _confirm = confirm;
            _confirmTimeout = confirmTimeout;
            _clusterStrategy = clusterStrategy;
            _clusterIdleTimout = clusterIdleTimout;
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _timerService.Add(this, OnTimer);

            _task = Task.Run(() => SendAsync(_cancellationTokenSource), CancellationToken.None); // do not cancel this task or messages will be lost

            _queue.Counters.Subscriptions.Add();
        }

        public void Dispose()
        {
            _queue.Counters.Subscriptions.Add(-1);
            StopProxyClient();
            _timerService.Remove(this);
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = null;
            _task = null;
        }

        private async Task SendAsync(CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await WaitQueueStartAsync(cancellationTokenSource.Token);

                    MessageEntity messageEntity = null;
                    
                    try
                    {
                        messageEntity = await _queue.TryDequeueAsync(cancellationTokenSource.Token);
                    }
                    catch (ServerException ex)
                    {
                        if (ex.ErrorCode != ErrorCode.QueueNotStarted)                        
                        {
                            throw;
                        }
                    }

                    if (messageEntity != null)
                    {
                        await ProcessMessageAsync(messageEntity, cancellationTokenSource.Token);

                        _lastSendDate = DateTime.UtcNow;
                        StopProxyClient();
                    }
                }
            }
            catch (Exception ex)
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    _client.Context.Logger.LogError(ex, "Unexpected error during sending messages from subscription.");
                }

                throw;
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }

        private async Task WaitQueueStartAsync(CancellationToken cancellationToken)
        {
            do
            {
                if (_queue.Status == QueueStatus.Started)
                {
                    break;
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

                    if (_queue.Status == QueueStatus.Deleted)
                    {
                        if (_queue.User.Queues.TryGetValue(_queue.Entity.Name, out var newQueue) &&
                            _queue != newQueue && newQueue.Status == QueueStatus.Started)
                        {
                            _queue.Counters.Subscriptions.Add(-1);
                            newQueue.Counters.Subscriptions.Add();
                            _queue = newQueue;

                            break;
                        }
                    }
                }
            } while (!cancellationToken.IsCancellationRequested);
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
            catch (ClientException ex)
            {
                response = FindRequestResponse(messageEntity, ex);
            }
            finally
            {
                if (messageEntity.Delivered)
                {
                    await DeletePersistentMessageAsync(messageEntity);
                }
                else
                {
                    _queue.Enqueue(messageEntity);
                }

                await SendRequestResponseAsync(messageEntity, response, cancellationToken);                
            }
        }

        private async Task<ReadOnlyMemory<byte>> LoadDiskOnlyDataAsync(MessageEntity messageEntity, CancellationToken cancellationToken)
        {
            var data = messageEntity.Data;

            if (data.Length == 0)
            {
                var diskEntity = await _client.Context.Storage.PersistentStorage.LoadMessageAsync(_queue.Entity.User,
                    _queue.Entity.Name, messageEntity.Id, true, cancellationToken);
                data = diskEntity.Data;
            }

            return data;
        }

        private static MessageResponse FindRequestResponse(MessageEntity messageEntity, ClientException ex)
        {
            MessageResponse result = null;

            if (messageEntity.Request)
            {
                if (ex.Response != null && ex.Response is MessageResponse response && response.Response)
                {
                    result = response;
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
            var result = messageEntity.ToCommand(_queue.Entity.Name);

            result.Confirm = _confirm;
            result.ConfirmTimeout = _confirmTimeout;
            result.Data = data;

            return result;
        }

        private async Task DeletePersistentMessageAsync(MessageEntity messageEntity)
        {
            if (messageEntity.Persistent != Persistence.No)
            {
                await _client.Context.Storage.PersistentStorage.DeleteMessageAsync(_queue.Entity.User,
                _queue.Entity.Name, messageEntity.Id, CancellationToken.None); // none cancellation to ensure operation is not interrupted
            }
        }

        private async Task<MessageResponse> SendMessageAsync(Message message, CancellationToken cancellationToken)
        {
            return await _client.SendAsync(message, cancellationToken);
        }

        private async Task SendRequestResponseAsync(MessageEntity messageEntity, MessageResponse result, CancellationToken cancellationToken)
        {
            if (messageEntity.Request && result != null && result.Response &&
                messageEntity.ClientId != null && _client.Context.Storage.TryGetClient(messageEntity.ClientId.Value, out var receiverClient))
            {
                var response = result.Copy();

                response.RequestId = messageEntity.Id;
                response.RequestTag = messageEntity.Tag;

                await receiverClient.SendAsync(response, cancellationToken);
            }
        }

        private void OnTimer()
        {
            try
            {
                if (_lastSendDate != null)
                {
                    IdleTime = DateTime.UtcNow.Subtract(_lastSendDate.Value);
                }

                if (_queue.Length == 0 && _client.Context.Storage.Cluster.Started
                    && (IdleTime == null || IdleTime > _clusterIdleTimout))
                {
                    switch (_clusterStrategy)
                    {
                        case ClusterStrategy.Proxy:
                            if (!_proxyStarted)
                            {
                                StartProxyAsync();
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
            catch (Exception ex)
            {
                _client.Context.Logger.LogError(ex, "Error on subscription idlness timer.");
            }
        }

        private void StartProxyAsync()
        {
            var hint = FindRedirectHint();

            if (hint != null)
            {
                var service = _client.Context.Storage.Service;

                var options = new NaiveMqClientOptions
                {
                    AutoRestart = false,
                    Hosts = hint.Host,
                    Username = service.Options.ClusterAdminUsername,
                    Password = service.Options.ClusterAdminPassword,
                    OnStart = ProxyClient_OnStart,
                    OnStop = ProxyClient_OnStop,
                };

                _proxyClient = new NaiveMqClient(options, service.ClientLogger, _cancellationTokenSource.Token);
                _proxyStarted = true;
            }
        }

        private void ProxyClient_OnStart(NaiveMqClient sender)
        {
            sender.OnReceiveMessageAsync += ProxyClient_OnReceiveMessageAsync;
            
            var subscribe = new Subscribe(
                _queue.Entity.Name,
                _confirm,
                _confirmTimeout,
                clusterStrategy: ClusterStrategy.Wait,
                user: _queue.Entity.User);

            sender.SendAsync(subscribe);
        }

        private async Task ProxyClient_OnReceiveMessageAsync(NaiveMqClient sender, Message message)
        {
            var result = await _client.SendAsync(message, true, false, _cancellationTokenSource.Token);

            if (message.Confirm)
            {
                await sender.SendAsync(result);
            }
        }

        private void ProxyClient_OnStop(NaiveMqClient sender)
        {
            StopProxyClient();
        }

        private void StopProxyClient()
        {
            if (_proxyClient != null)
            {
                _proxyClient.Dispose();
                _proxyClient = null;
                _proxyStarted = false;
            }
        }

        private async Task SendRedirectCommandAsync()
        {
            var hint = FindRedirectHint();

            if (hint != null)
            {
                await _client.SendAsync(new ClusterRedirect(hint.Host) { Confirm = false });
            }
        }

        private async Task SendHintCommandAsync()
        {
            var hints = GetQueueHints().ToList();

            if (hints.Any())
            {
                await _client.SendAsync(new ClusterHint(hints) { Confirm = false });
            }
        }

        private IEnumerable<QueueHint> GetQueueHints()
        {
            foreach (var server in _client.Context.Storage.Cluster.Servers.Values)
            {
                if (server.Name != _client.Context.Storage.Cluster.Self.Name
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

        private QueueHint FindRedirectHint()
        {
            var hints = GetQueueHints().Where(x => x.Subscriptions < _queue.Counters.Subscriptions.Value).ToList();

            if (hints.Any())
            {
                return hints.OrderBy(x => x.Subscriptions).ThenByDescending(x => x.Length).First();
            }
            else
            {
                return null;
            }
        }
    }
}