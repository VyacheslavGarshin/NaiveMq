using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Common;
using NaiveMq.Service.PersistentStorage;
using System.Collections.Concurrent;
using static NaiveMq.Service.Cogs.UserCog;
using static NaiveMq.Service.NaiveMqService;

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
        
        private readonly ConcurrentDictionary<int, ClientContext> _clientContexts = new();

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

            foreach (var context in _clientContexts.Values)
            {
                context.Dispose();
            }

            _clientContexts.Clear();

            if (PersistentStorage != null)
            {
                // we don't manage lifecycle of persistence storage
                PersistentStorage = null;
            }
        }

        public bool TryGetClientContext(int id, out ClientContext clientContext)
        {
            return _clientContexts.TryGetValue(id, out clientContext);
        }

        public bool TryAddClient(NaiveMqClient client)
        {
            var clientContex = new ClientContext
            {
                Storage = this,
                Client = client,
                StoppingToken = _stoppingToken,
                Logger = _logger
            };

            var result = _clientContexts.TryAdd(client.Id, clientContex);

            _logger.LogInformation("Client added '{ClientId}' from endpoint {RemoteEndPoint}.", client.Id, client.TcpClient.Client.RemoteEndPoint);

            return result;
        }

        public bool TryRemoveClient(NaiveMqClient client)
        {
            var result = _clientContexts.TryRemove(client.Id, out var clientContext);

            if (result)
            {
                clientContext.Dispose();
                _logger.LogInformation("Client deleted '{ClientId}'.", client.Id);
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

        public class StorageCounters : UserCounters
        {
            public StorageCounters(SpeedCounterService service) : base(service)
            {
            }

            public StorageCounters(SpeedCounterService service, ServiceCounters parent) : base(service)
            {
                Read.Parent = parent.Read;
                Write.Parent = parent.Write;
            }
        }
    }
}
