using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Service.Counters;
using NaiveMq.Service.PersistentStorage;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class Storage : IDisposable
    {
        public NaiveMqService Service { get; }

        public IPersistentStorage PersistentStorage { get; set; }

        public bool MemoryLimitExceeded { get; private set; }

        public Cluster Cluster { get; private set; }

        public ConcurrentDictionary<string, UserCog> Users { get; } = new(StringComparer.InvariantCultureIgnoreCase);

        public StorageCounters Counters { get; }

        private readonly NaiveMqServiceOptions _options;
        
        private readonly ConcurrentDictionary<int, NaiveMqClientWithContext> _clients = new();

        private readonly CancellationToken _stoppingToken;

        private readonly ILogger<NaiveMqService> _logger;

        private readonly ILogger<NaiveMqClient> _clientLogger;

        private readonly Timer _oneSecondTimer;
        
        public Storage(NaiveMqService service, IPersistentStorage persistentStorage, ILogger<NaiveMqService> logger, ILogger<NaiveMqClient> clientLogger, CancellationToken stoppingToken)
        {
            Service = service;
            _options = service.Options;
            _logger = logger;
            _clientLogger = clientLogger;
            _stoppingToken = stoppingToken;

            PersistentStorage = persistentStorage;
            Cluster = new Cluster(this, logger, clientLogger, stoppingToken);

            Counters = new(service.SpeedCounterService, service.Counters);

            _oneSecondTimer = new(OnTimer, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));
        }

        public void Dispose()
        {
            _oneSecondTimer.Dispose();

            Cluster.Dispose();

            foreach (var user in Users.Values)
            {
                user.Dispose();
            }

            Users.Clear();

            foreach (var context in _clients.Values)
            {
                context.Dispose();
            }

            _clients.Clear();

            if (PersistentStorage != null)
            {
                // we don't manage lifecycle of persistence storage
                PersistentStorage = null;
            }
        }

        public bool TryGetClient(int id, out NaiveMqClientWithContext client)
        {
            return _clients.TryGetValue(id, out client);
        }

        public bool TryAddClient(NaiveMqClientWithContext client)
        {
            var result = _clients.TryAdd(client.Id, client);

            _logger.LogTrace("Client added '{ClientId}' from endpoint {RemoteEndPoint}.", client.Id, client.TcpClient.Client.RemoteEndPoint);

            return result;
        }

        public bool TryRemoveClient(NaiveMqClientWithContext client)
        {
            var result = _clients.TryRemove(client.Id, out _);

            if (result)
            {
                try
                {
                    client.Dispose();
                    _logger.LogTrace("Client deleted '{ClientId}'.", client.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while deleting client '{ClientId}'.", client.Id);
                }
            }

            return result;
        }

        private void OnTimer(object state)
        {
            UpdateMemoryLimitExceeded();
        }

        private void UpdateMemoryLimitExceeded()
        {
            var memoryInfo = GC.GetGCMemoryInfo();
            var freeMemory = memoryInfo.HighMemoryLoadThresholdBytes - memoryInfo.MemoryLoadBytes + memoryInfo.HeapSizeBytes;
            MemoryLimitExceeded = freeMemory < 0.01 * (100 - _options.AutoMemoryLimitPercent) * memoryInfo.HighMemoryLoadThresholdBytes
                || (_options.MemoryLimit != null && memoryInfo.HeapSizeBytes > _options.MemoryLimit);
        }
    }
}
