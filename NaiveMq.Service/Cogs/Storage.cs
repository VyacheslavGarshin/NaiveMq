using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Service.PersistentStorage;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class Storage : IDisposable
    {
        public IPersistentStorage PersistentStorage { get; set; }

        public bool MemoryLimitExceeded { get; private set; }

        public readonly ConcurrentDictionary<string, UserCog> Users = new(StringComparer.InvariantCultureIgnoreCase);

        public readonly ConcurrentDictionary<int, ConcurrentDictionary<QueueCog, SubscriptionCog>> Subscriptions = new();

        private readonly ConcurrentDictionary<int, ClientContext> _clientContexts = new();

        private readonly CancellationToken _stoppingToken;

        private readonly ILogger _logger;

        private readonly Timer _timer;

        private readonly NaiveMqServiceOptions _options;

        public Storage(NaiveMqServiceOptions options, IPersistentStorage persistentStorage, ILogger logger, CancellationToken stoppingToken)
        {
            _options = options;
            _logger = logger;
            _stoppingToken = stoppingToken;

            PersistentStorage = persistentStorage;

            _timer = new(OnTimer, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));
        }

        public void DeleteSubscriptions(int clientId)
        {
            if (Subscriptions.TryRemove(clientId, out var clientSubscriptions))
            {
                foreach (var subscription in clientSubscriptions)
                {
                    subscription.Value.Dispose();
                }

                clientSubscriptions.Clear();
            };
        }

        public void Dispose()
        {
            _timer.Dispose();

            foreach (var clientSubscriptions in Subscriptions)
            {
                DeleteSubscriptions(clientSubscriptions.Key);
            }

            Subscriptions.Clear();

            foreach (var user in Users.Values)
            {
                user.Dispose();
            }

            foreach (var context in _clientContexts.Values)
            {
                context.Client.Dispose();
            }

            _clientContexts.Clear();

            if (PersistentStorage != null)
            {
                // we don't manage lifecycle of persistence storage
                PersistentStorage = null;
            }
        }

        public bool TryGetClient(int id, out ClientContext clientContext)
        {
            return _clientContexts.TryGetValue(id, out clientContext);
        }

        public void AddClient(NaiveMqClient client)
        {
            _clientContexts.TryAdd(client.Id, new ClientContext
            {
                Storage = this,
                User = null,
                Client = client,
                StoppingToken = _stoppingToken,
                Logger = _logger
            });


            _logger.LogInformation($"Client added {client.Id}.");
        }

        public void DeleteClient(NaiveMqClient client)
        {
            DeleteSubscriptions(client.Id);

            _clientContexts.TryRemove(client.Id, out var _);
            client.Dispose();

            _logger.LogInformation($"Client deleted {client.Id}.");
        }

        private void OnTimer(object state)
        {
            UpdateMemoryLimitExceeded();
        }

        private void UpdateMemoryLimitExceeded()
        {
            var memoryInfo = GC.GetGCMemoryInfo();
            var freeMemory = memoryInfo.HighMemoryLoadThresholdBytes - memoryInfo.MemoryLoadBytes;
            MemoryLimitExceeded = ((double)freeMemory / memoryInfo.HighMemoryLoadThresholdBytes) < 0.1
                || (_options.MemoryLimit != null && memoryInfo.HeapSizeBytes > _options.MemoryLimit);
        }
    }
}
