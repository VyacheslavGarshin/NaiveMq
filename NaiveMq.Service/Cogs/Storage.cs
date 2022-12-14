using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Common;
using NaiveMq.Service.PersistentStorage;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class Storage : IDisposable
    {
        public SpeedCounter WriteCounter { get; set; } = new(10);

        public SpeedCounter ReadCounter { get; set; } = new(10);

        public SpeedCounter ReadMessageCounter { get; set; } = new(10);

        public SpeedCounter WriteMessageCounter { get; set; } = new(10);

        public IPersistentStorage PersistentStorage { get; set; }

        public bool MemoryLimitExceeded { get; private set; }

        public ConcurrentDictionary<string, UserCog> Users { get; } = new(StringComparer.InvariantCultureIgnoreCase);

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

        public void Dispose()
        {
            _timer.Dispose();

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

            ReadCounter.Dispose();
            WriteCounter.Dispose();
            ReadMessageCounter.Dispose();
            WriteMessageCounter.Dispose();
        }

        public bool TryGetClient(int id, out ClientContext clientContext)
        {
            return _clientContexts.TryGetValue(id, out clientContext);
        }

        public bool TryAddClient(NaiveMqClient client)
        {
            var result = _clientContexts.TryAdd(client.Id, new ClientContext
            {
                Storage = this,
                Client = client,
                StoppingToken = _stoppingToken,
                Logger = _logger
            });

            _logger.LogInformation($"Client added {client.Id}.");

            return result;
        }

        public bool TryRemoveClient(NaiveMqClient client)
        {
            var result = _clientContexts.TryRemove(client.Id, out var clientContext);

            if (result)
            {
                clientContext.Dispose();

                _logger.LogInformation($"Client deleted {client.Id}.");
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
            var freeMemory = memoryInfo.HighMemoryLoadThresholdBytes - memoryInfo.MemoryLoadBytes;
            MemoryLimitExceeded = ((double)freeMemory / memoryInfo.HighMemoryLoadThresholdBytes) < 0.1
                || (_options.MemoryLimit != null && memoryInfo.HeapSizeBytes > _options.MemoryLimit);
        }
    }
}
