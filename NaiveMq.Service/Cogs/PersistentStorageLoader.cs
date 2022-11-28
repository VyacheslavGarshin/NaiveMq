using Microsoft.Extensions.Logging;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Entities;
using NaiveMq.Service.Handlers;

namespace NaiveMq.Service.Cogs
{
    public class PersistentStorageLoader
    {
        private Storage _storage;
        
        private ILogger _logger;

        private CancellationToken _cancellationToken;

        public PersistentStorageLoader(Storage storage, ILogger logger, CancellationToken cancellationToken)
        {
            _storage = storage;
            _logger = logger;
            _cancellationToken = cancellationToken;
        }

        public async Task Load()
        {
            try
            {
                if (_storage.PersistentStorage != null)
                {
                    _logger.LogInformation("Starting to load users, persistent queues and messages.");

                    await LoadUsers();
                    var allQueues = new Dictionary<string, IEnumerable<QueueEntity>>();
                    await LoadQueues(allQueues);
                    await LoadBindings();
                    await LoadMessagesAsync(allQueues);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading persistent data.");
                
                throw;
            }
        }

        private async Task LoadUsers()
        {
            var result = (await _storage.PersistentStorage.LoadUsersAsync(_cancellationToken)).ToList();

            var context = new ClientContext { Logger = _logger, Storage = _storage, Reinstate = true, CancellationToken = _cancellationToken };

            if (result.Any())
            {
                foreach (var user in result)
                {
                    await new AddUserHandler().ExecuteAsync(context, new AddUser { Username = user.Username, Password = user.PasswordHash, Administrator = user.Administrator });
                }

                _logger.LogInformation($"{result.Count} users are loaded.");
            }
            else
            {
                _logger.LogInformation($"There are no users in the persistent storage. Adding default user.");

                context.Reinstate = false;

                await new AddUserHandler().ExecuteAsync(context, new AddUser { Username = "guest", Password = "guest", Administrator = true });

                _logger.LogInformation($"Added default 'guest' user with the same password.");
            }
        }

        private async Task LoadQueues(Dictionary<string, IEnumerable<QueueEntity>> allQueues)
        {
            var queuesCount = 0;

            foreach (var user in _storage.Users.Values)
            {
                var queues = (await _storage.PersistentStorage.LoadQueuesAsync(user.Username, _cancellationToken)).ToList();

                allQueues[user.Username] = queues;

                var context = new ClientContext { User = user, Logger = _logger, Storage = _storage, Reinstate = true, CancellationToken = _cancellationToken };

                foreach (var queue in queues)
                {
                    await new AddQueueHandler().ExecuteAsync(context, new AddQueue { Name = queue.Name, Durable = queue.Durable, IsExchange = queue.IsExchange });
                    queuesCount++;
                }
            }

            _logger.LogInformation($"{queuesCount} persistent queues are loaded.");
        }

        private async Task LoadBindings()
        {
            var bindingsCount = 0;

            foreach (var user in _storage.Users.Values)
            {
                var bindings = (await _storage.PersistentStorage.LoadBindingsAsync(user.Username, _cancellationToken)).ToList();

                var context = new ClientContext { User = user, Logger = _logger, Storage = _storage, Reinstate = true, CancellationToken = _cancellationToken };

                foreach (var binding in bindings)
                {
                    await new AddBindingHandler().ExecuteAsync(context, new AddBinding { Exchange = binding.Exchange, Queue = binding.Queue, Durable = binding.Durable, Regex = binding.Regex });
                    bindingsCount++;
                }
            }

            _logger.LogInformation($"{bindingsCount} persistent bindings are loaded.");
        }

        private async Task LoadMessagesAsync(Dictionary<string, IEnumerable<QueueEntity>> allQueues)
        {
            var messageCount = 0;

            foreach (var pair in allQueues)
            {
                var context = new ClientContext { User = new UserEntity { Username = pair.Key }, Logger = _logger, Storage = _storage, Reinstate = true, CancellationToken = _cancellationToken };

                foreach (var queue in pair.Value)
                {
                    var messages = await _storage.PersistentStorage.LoadMessagesAsync(queue.User, queue.Name, _cancellationToken);

                    foreach (var message in messages)
                    {
                        await new MessageHandler().ExecuteAsync(context, new Message { Id = message.Id, Queue = message.Queue, Durable = message.Durable, Text = message.Text });
                        messageCount++;
                    }
                }
            }

            _logger.LogInformation($"{messageCount} persistent messages are loaded.");
        }
    }
}
