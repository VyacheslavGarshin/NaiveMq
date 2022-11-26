using NaiveMq.Client;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Service.PersistentStorage;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class Storage : IDisposable
    {
        public IPersistentStorage PersistentStorage { get; set; }

        public readonly ConcurrentDictionary<string, UserEntity> Users = new(StringComparer.InvariantCultureIgnoreCase);

        public readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Queue>> UserQueues = new(StringComparer.InvariantCultureIgnoreCase);

        public readonly ConcurrentDictionary<NaiveMqClient, ConcurrentDictionary<Queue, Subscription>> Subscriptions = new();

        public ConcurrentDictionary<string, Queue> GetUserQueues(HandlerContext context)
        {
            if (!UserQueues.TryGetValue(context.User.Username, out var userQueues))
            {
                throw new ServerException(ErrorCode.UserQueuesNotFound, string.Format(ErrorCode.UserQueuesNotFound.GetDescription(), context.User.Username));
            }

            return userQueues;
        }

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

            foreach (var userQueues in UserQueues)
            {
                foreach (var queue in userQueues.Value)
                {
                    queue.Value.Dispose();
                }

                userQueues.Value.Clear();
            }

            UserQueues.Clear();

            if (PersistentStorage != null)
            {
                // we don't manage lifecycle of persistence storage
                PersistentStorage = null;
            }
        }
    }
}
