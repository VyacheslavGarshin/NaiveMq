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
                
                var handler = new AddUserHandler();
                await handler.ExecuteEntityAsync(context, user);
                
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

                    using var handler = new AddQueueHandler();
                    await handler.ExecuteEntityAsync(context, queue);

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
                    var binding = await _storage.PersistentStorage.LoadBindingAsync(user.Username, key, _cancellationToken);

                    using var handler = new AddBindingHandler();
                    await handler.ExecuteEntityAsync(context, binding);

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

                    var messages = (await _storage.PersistentStorage.LoadMessageKeysAsync(queue.Entity.User, queue.Entity.Name, _cancellationToken)).ToList();
                    var queueMessageCount = 0;

                    foreach (var key in messages)
                    {
                        var message = await _storage.PersistentStorage.LoadMessageAsync(user.Username, queue.Entity.Name, key, _cancellationToken);

                        if (message != null)
                        {
                            using var handler = new MessageHandler();
                            await handler.ExecuteEntityAsync(context, message);

                            messageCount++;
                        }

                        queueMessageCount++;

                        if (sw.Elapsed > TimeSpan.FromSeconds(10))
                        {
                            _logger.LogInformation($"{messageCount} persistent messages are loaded. {queueMessageCount}/{messages.Count} loaded for queue '{queue.Entity.Name}'.");
                            sw.Restart();
                        }
                    }
                }
            }

            _logger.LogInformation($"{messageCount} persistent messages are loaded.");
        }
    }
}
