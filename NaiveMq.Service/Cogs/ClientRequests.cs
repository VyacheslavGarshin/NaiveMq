using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Common;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class ClientRequests : IDisposable
    {
        private class DeadlineItem
        {
            public int ClientId { get; set; }
            
            public Guid MessageId { get; set; }
        }

        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(10);

        private ConcurrentDictionary<int, ConcurrentDictionary<Guid, DeadlineItem>> _clientRequests = new();

        private ConcurrentDictionary<Guid, int> _requestClient = new();

        private SortedDictionary<DateTime, ConcurrentBag<DeadlineItem>> _deadlines = new();

        private object _deadlinesLocker = new { };
        
        private readonly ILogger _logger;
        
        private readonly Timer _timer;

        public ClientRequests(ILogger logger)
        {
            _logger = logger;
            _timer = new Timer(OnTimer, null, 1000, 1000);
        }

        public void AddRequest(int clientId, Guid messageId, TimeSpan? timeout)
        {
            var clientRequests = _clientRequests.GetOrAdd(clientId, (x) => new ConcurrentDictionary<Guid, DeadlineItem>());
            var timeoutItem = new DeadlineItem { ClientId = clientId, MessageId = messageId };

            if (clientRequests.TryAdd(messageId, timeoutItem) && _requestClient.TryAdd(messageId, clientId))
            {
                var deadline = DateTime.Now.Add(timeout ?? DefaultTimeout);
                ConcurrentBag<DeadlineItem> deadlineItems;
                
                lock (_deadlinesLocker)
                {
                    if (!_deadlines.TryGetValue(deadline, out deadlineItems))
                    {
                        deadlineItems = new();
                        _deadlines.Add(deadline, deadlineItems);
                    }
                }

                deadlineItems.Add(timeoutItem);
            }
            else
            {
                throw new ServerException(ErrorCode.RequestAlreadyRegistered, string.Format(ErrorCode.RequestAlreadyRegistered.GetDescription(), messageId));
            }
        }

        public int? RemoveRequest(Guid messageId)
        {
            if (_requestClient.TryRemove(messageId, out var clientId) && _clientRequests.TryGetValue(clientId, out var clientRequests))
            {
                return clientRequests.TryRemove(messageId, out var _) ? clientId : null;
            }
            else
            {
                return null;
            }
        }

        public void RemoveClient(int clientId)
        {
            if (_clientRequests.TryRemove(clientId, out var clientRequests))
            {
                foreach (var request in clientRequests.Values)
                {
                    _requestClient.TryRemove(request.MessageId, out var _);
                }               
            };
        }

        private void OnTimer(object state)
        {
            try
            {
                while (true)
                {
                    var deadline = _deadlines.FirstOrDefault();

                    if (deadline.Value != null && deadline.Key < DateTime.Now)
                    {
                        foreach (var item in deadline.Value)
                        {
                            RemoveRequest(item.MessageId);
                        }

                        _deadlines.Remove(deadline.Key);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clearing old requests.");
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
