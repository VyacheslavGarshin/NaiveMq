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
        public ConcurrentDictionary<string, ActiveQueue> ActiveQueues { get; private set; } = new(StringComparer.InvariantCultureIgnoreCase);

        public void ReplaceActiveQueues(IEnumerable<ActiveQueue> queueStats)
        {
            var newOne = new ConcurrentDictionary<string, ActiveQueue>(StringComparer.InvariantCultureIgnoreCase);
            
            foreach (var stat in queueStats)
            {
                newOne.TryAdd(stat.Key, stat);
            }

            ActiveQueues = newOne;
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
