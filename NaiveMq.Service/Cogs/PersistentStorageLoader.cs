using Microsoft.Extensions.Logging;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Handlers;
using System.Diagnostics;

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

        public async Task LoadAsync()
        {
            try
            {
                if (_storage.PersistentStorage != null)
                {
                    _logger.LogInformation("Starting to load users, persistent queues and messages.");

                    await LoadUsersAsync();
                    await LoadQueuesAsync();
                    await LoadBindingsAsync();
                    await LoadMessagesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading persistent data.");
                
                throw;
            }
        }

        private async Task LoadUsersAsync()
        {
            var context = new ClientContext { Logger = _logger, Storage = _storage, Reinstate = true, CancellationToken = _cancellationToken };

            var count = 0;     
            
            foreach (var key in await _storage.PersistentStorage.LoadUserKeysAsync(_cancellationToken))
            {
                var user = await _storage.PersistentStorage.LoadUserAsync(key, _cancellationToken);
                await new AddUserHandler().ExecuteAsync(context, new AddUser { Id = Guid.NewGuid(), Username = user.Username, Password = user.PasswordHash, Administrator = user.Administrator });
                count++;
            }

            if (count > 0)
            {
                _logger.LogInformation($"{count} users are loaded.");
            }
            else
            {
                _logger.LogInformation($"There are no users in the persistent storage. Adding default user.");

                context.Reinstate = false;

                await new AddUserHandler().ExecuteAsync(context, new AddUser { Username = "guest", Password = "guest", Administrator = true });

                _logger.LogInformation($"Added default 'guest' user with the same password.");
            }
        }

        private async Task LoadQueuesAsync()
        {
            var queuesCount = 0;

            foreach (var user in _storage.Users.Values)
            {
                var context = new ClientContext { User = user, Logger = _logger, Storage = _storage, Reinstate = true, CancellationToken = _cancellationToken };

                foreach (var keys in await _storage.PersistentStorage.LoadQueueKeysAsync(user.Username, _cancellationToken))
                {
                    var queue = await _storage.PersistentStorage.LoadQueueAsync(user.Username, keys, _cancellationToken);
                    await new AddQueueHandler().ExecuteAsync(context, new AddQueue { Id = Guid.NewGuid(), Name = queue.Name, Durable = queue.Durable, Exchange = queue.Exchange });
                    queuesCount++;
                }
            }

            _logger.LogInformation($"{queuesCount} persistent queues are loaded.");
        }

        private async Task LoadBindingsAsync()
        {
            var bindingsCount = 0;

            foreach (var user in _storage.Users.Values)
            {
                var bindings = (await _storage.PersistentStorage.LoadBindingKeysAsync(user.Username, _cancellationToken)).ToList();

                var context = new ClientContext { User = user, Logger = _logger, Storage = _storage, Reinstate = true, CancellationToken = _cancellationToken };

                foreach (var key in bindings)
                {
                    var binding = await _storage.PersistentStorage.LoadBindingAsync(user.Username, key.Exchange, key.Queue, _cancellationToken);

                    await new AddBindingHandler().ExecuteAsync(context, new AddBinding { Id = Guid.NewGuid(), Exchange = binding.Exchange, Queue = binding.Queue, Durable = binding.Durable, Pattern = binding.Pattern });
                    bindingsCount++;
                }
            }

            _logger.LogInformation($"{bindingsCount} persistent bindings are loaded.");
        }

        private async Task LoadMessagesAsync()
        {
            var messageCount = 0;
            var sw = new Stopwatch();
            sw.Start();

            foreach (var user in _storage.Users.Values)
            {
                foreach (var queue in _storage.UserQueues[user.Username].Values)
                {
                    var context = new ClientContext { User = user, Logger = _logger, Storage = _storage, Reinstate = true, CancellationToken = _cancellationToken };

                    var messages = (await _storage.PersistentStorage.LoadMessageKeysAsync(queue.User, queue.Name, _cancellationToken)).ToList();
                    var queueMessageCount = 0;

                    foreach (var key in messages)
                    {
                        var message = await _storage.PersistentStorage.LoadMessageAsync(user.Username, queue.Name, key, _cancellationToken);

                        if (message != null)
                        {
                            var messageCommand = new Message
                            {
                                Id = message.Id,
                                Queue = message.Queue,
                                Request = message.Request,
                                Persistent = message.Persistent,
                                RoutingKey = message.RoutingKey,
                                Data = message.Data
                            };

                            await new MessageHandler().ExecuteAsync(context, messageCommand);
                            messageCount++;
                        }

                        queueMessageCount++;

                        if (sw.Elapsed > TimeSpan.FromSeconds(10))
                        {
                            _logger.LogInformation($"{messageCount} persistent messages are loaded. {queueMessageCount}/{messages.Count} loaded for queue '{queue.Name}'.");
                            sw.Restart();
                        }
                    }
                }
            }

            _logger.LogInformation($"{messageCount} persistent messages are loaded.");
        }
    }
}
