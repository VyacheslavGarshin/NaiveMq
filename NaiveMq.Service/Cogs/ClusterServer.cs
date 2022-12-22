using NaiveMq.Client.Dto;
using NaiveMq.Client;
using NaiveMq.Service.Dto;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class ClusterServer : IDisposable
    {
        public Host Host { get; set; }

        public string Name { get; set; }

        public bool Self { get; set; }

        public NaiveMqClient Client { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Key is User:Queue.</remarks>
        public ConcurrentDictionary<string, QueueStats> UserQueueStats { get; private set; } = new(StringComparer.InvariantCultureIgnoreCase);

        public void ReplaceUserQueueStats(IEnumerable<QueueStats> userQueueStats)
        {
            var newOne = new ConcurrentDictionary<string, QueueStats>(StringComparer.InvariantCultureIgnoreCase);
            
            foreach (var stat in userQueueStats)
            {
                newOne.TryAdd(stat.Key, stat);
            }

            UserQueueStats = newOne;
        }

        public void Dispose()
        {
            if (Client != null)
            {
                Client.Dispose();
            }
        }
    }
}
