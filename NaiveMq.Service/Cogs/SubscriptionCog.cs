﻿using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Dto;
using NaiveMq.Service.Entities;
using NaiveMq.Service.Enums;
using NaiveMq.Service.Handlers;
using System.Threading;

namespace NaiveMq.Service.Cogs
{
    public class SubscriptionCog : IDisposable
    {
        private static readonly TimerService _timerService = new(TimeSpan.FromSeconds(1));

        public TimeSpan? IdleTime { get; private set; }

        private DateTime? _lastSendDate;

        private DateTime? _lastRedirectSendDate;
        
        private DateTime? _lastHintSendDate;

        private bool _proxyStarted;

        private QueueCog _queue;

        private readonly bool _confirm;

        private readonly TimeSpan? _confirmTimeout;

        private readonly ClusterStrategy _clusterStrategy;

        private readonly TimeSpan _clusterIdleTimout;

        private readonly ClientContext _context;

        private CancellationTokenSource _cancellationTokenSource;

        private Task _task;

        private NaiveMqClient _proxyClient;

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
            _cancellationTokenSource = new CancellationTokenSource();
            _timerService.Add(this, OnTimer);

            _task = Task.Run(() => SendAsync(_cancellationTokenSource), CancellationToken.None); // do not cancel this task or messages will be lost

            _queue.Counters.Subscriptions.Add();
        }

        public void Dispose()
        {
            _queue.Counters.Subscriptions.Add(-1);
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = null;
            _task = null;
            _timerService.Remove(this);
            StopProxyClient();
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
                    _context.Logger.LogError(ex, "Unexpected error during sending messages from subscription.");
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
                    await ReEnqueueMessageAsync(messageEntity);
                }

                await SendRequestResponseAsync(messageEntity, response, cancellationToken);                
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
            var result = messageEntity.ToCommand();

            result.Confirm = _confirm;
            result.ConfirmTimeout = _confirmTimeout;
            result.Data = data;

            return result;
        }

        private async Task DeletePersistentMessageAsync(MessageEntity messageEntity)
        {
            if (messageEntity.Persistent != Persistence.No)
            {
                await _context.Storage.PersistentStorage.DeleteMessageAsync(_queue.Entity.User,
                _queue.Entity.Name, messageEntity.Id, CancellationToken.None); // none cancellation to ensure operation is not interrupted
            }
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
            if (messageEntity.Request && result != null && result.Response &&
                messageEntity.ClientId != null && _context.Storage.TryGetClientContext(messageEntity.ClientId.Value, out var receiverContext))
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

        private void StartProxyAsync()
        {
            var hint = FindRedirectHint();

            if (hint != null)
            {
                var service = _context.Storage.Service;

                var options = new NaiveMqClientOptions
                {
                    AutoRestart = false,
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
            _proxyClient.OnReceiveMessageAsync += ProxyClient_OnReceiveMessageAsync;
            _proxyClient.SendAsync(new Subscribe(_queue.Entity.Name, clusterStrategy: ClusterStrategy.Wait));
        }

        private async Task ProxyClient_OnReceiveMessageAsync(NaiveMqClient sender, Message message)
        {
            await _context.Client.SendAsync(message, true, _cancellationTokenSource.Token);
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
                await _context.Client.SendAsync(new ClusterRedirect(hint.Host) { Confirm = false });
            }
        }

        private async Task SendHintCommandAsync()
        {
            var hints = GetQueueHints().ToList();

            if (hints.Any())
            {
                await _context.Client.SendAsync(new ClusterHint(hints) { Confirm = false });
            }
        }

        private IEnumerable<QueueHint> GetQueueHints()
        {
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