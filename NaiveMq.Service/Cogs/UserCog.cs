using NaiveMq.Service.Entities;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class UserCog : IDisposable
    {
        public UserEntity Entity { get; set; }

        public ConcurrentDictionary<string, QueueCog> Queues { get; } = new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Keys are: from queue, to queue.</remarks>
        public ConcurrentDictionary<string, ConcurrentDictionary<string, BindingCog>> Bindings { get; }  = new(StringComparer.InvariantCultureIgnoreCase);

        public UserCog(UserEntity entity)
        {
            Entity = entity;
        }

        public void Dispose()
        {
            foreach (var queue in Queues.Values)
            {
                queue.Dispose();
            }
        }
    }
}
