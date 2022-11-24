using NaiveMq.Client;
using NaiveMq.Service.PersistentStorage;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class Storage : IDisposable
    {
        public IPersistentStorage PersistentStorage { get; set; }

        public readonly ConcurrentDictionary<string, Queue> Queues = new(StringComparer.InvariantCultureIgnoreCase);

        public readonly ConcurrentDictionary<NaiveMqClient, ConcurrentDictionary<Queue, Subscription>> Subscriptions = new();

        public void DeleteSubscriptions(NaiveMqClient client)
        {
            if (Subscriptions.TryRemove(client, out var subscriptions))
            {
                foreach (var subscription in subscriptions)
                {
                    subscription.Value.Dispose();
                }

                subscriptions.Clear();
            };
        }

        public void Dispose()
        {
            foreach (var clientSubscriptions in Subscriptions)
            {
                DeleteSubscriptions(clientSubscriptions.Key);
            }

            Subscriptions.Clear();

            foreach (var queue in Queues)
            {
                queue.Value.Dispose();
            }

            Queues.Clear();

            if (PersistentStorage != null)
            {
                // we don't manage lifecycle of persistence storage
                PersistentStorage = null;
            }
        }
    }
}
