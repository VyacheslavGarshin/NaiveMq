using Microsoft.Extensions.Logging;
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

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Keys are: user, from queue, to queue.</remarks>
        public readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, Binding>>> UserBindings = new(StringComparer.InvariantCultureIgnoreCase);

        public readonly ConcurrentDictionary<int, ConcurrentDictionary<Queue, Subscription>> Subscriptions = new();

        public ClientRequests ClientRequests { get; set; }

        private readonly ConcurrentDictionary<int, ClientContext> _clientContexts = new();

        private readonly CancellationToken _stoppingToken;

        private readonly ILogger _logger;

        public Storage(IPersistentStorage persistentStorage, ILogger logger, CancellationToken stoppingToken)
        {
            _logger = logger;
            _stoppingToken = stoppingToken;

            PersistentStorage = persistentStorage;
            ClientRequests = new ClientRequests(_logger);
        }

        public ConcurrentDictionary<string, Queue> GetUserQueues(ClientContext context)
        {
            if (!UserQueues.TryGetValue(context.User.Username, out var userQueues))
            {
                throw new ServerException(ErrorCode.UserQueuesNotFound, string.Format(ErrorCode.UserQueuesNotFound.GetDescription(), context.User.Username));
            }

            return userQueues;
        }

        public ConcurrentDictionary<string, ConcurrentDictionary<string, Binding>> GetUserBindings(ClientContext context)
        {
            if (!UserBindings.TryGetValue(context.User.Username, out var userBindings))
            {
                throw new ServerException(ErrorCode.UserBindingsNotFound, string.Format(ErrorCode.UserBindingsNotFound.GetDescription(), context.User.Username));
            }

            return userBindings;
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
            ClientRequests.Dispose();

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
                CancellationToken = _stoppingToken,
                Logger = _logger
            });


            _logger.LogInformation($"Client added {client.Id}.");
        }

        public void DeleteClient(NaiveMqClient client)
        {
            DeleteSubscriptions(client.Id);
            ClientRequests.RemoveClient(client.Id);

            _clientContexts.TryRemove(client.Id, out var _);
            client.Dispose();

            _logger.LogInformation($"Client deleted {client.Id}.");
        }
    }
}
