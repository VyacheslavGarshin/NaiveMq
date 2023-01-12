using Microsoft.Extensions.Logging;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Enums;
using NaiveMq.Service.Handlers;
using System.Diagnostics;

namespace NaiveMq.Service.Cogs
{
    public class PersistentStorageLoader
    {
        private readonly Storage _storage;
        
        private readonly ILogger _logger;

        private readonly CancellationToken _cancellationToken;

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
            using var context = new ClientContext { Logger = _logger, Storage = _storage, Mode = ClientContextMode.Reinstate };

            var count = 0;     
            
            foreach (var key in await _storage.PersistentStorage.LoadUserKeysAsync(_cancellationToken))
            {
                var user = await _storage.PersistentStorage.LoadUserAsync(key, _cancellationToken);
                
                await AddUserHandler.ExecuteEntityAsync(context, user, null, _cancellationToken);
                
                count++;
            }

            if (count > 0)
            {
                _logger.LogInformation("{Count} users are loaded.", count);
            }
            else
            {
                _logger.LogInformation($"There are no users in the persistent storage. Adding default user.");

                context.Mode = ClientContextMode.Init;
                await new AddUserHandler().ExecuteAsync(context, new AddUser { Username = "guest", Password = "guest", Administrator = true }, _cancellationToken);

                _logger.LogInformation($"Added default 'guest' user with the same password.");
            }
        }

        private async Task LoadQueuesAsync()
        {
            var queuesCount = 0;

            foreach (var user in _storage.Users.Values)
            {
                using var context = new ClientContext { User = user, Logger = _logger, Storage = _storage, Mode = ClientContextMode.Reinstate };

                foreach (var keys in await _storage.PersistentStorage.LoadQueueKeysAsync(user.Entity.Username, _cancellationToken))
                {
                    var queue = await _storage.PersistentStorage.LoadQueueAsync(user.Entity.Username, keys, _cancellationToken);

                    await AddQueueHandler.ExecuteEntityAsync(context, queue, null, _cancellationToken);

                    queuesCount++;
                }
            }

            _logger.LogInformation("{QueuesCount} persistent queues are loaded.", queuesCount);
        }

        private async Task LoadBindingsAsync()
        {
            var bindingsCount = 0;

            foreach (var user in _storage.Users.Values)
            {
                var bindings = (await _storage.PersistentStorage.LoadBindingKeysAsync(user.Entity.Username, _cancellationToken)).ToList();

                using var context = new ClientContext { User = user, Logger = _logger, Storage = _storage, Mode = ClientContextMode.Reinstate };

                foreach (var key in bindings)
                {
                    var binding = await _storage.PersistentStorage.LoadBindingAsync(user.Entity.Username, key, _cancellationToken);

                    await AddBindingHandler.ExecuteEntityAsync(context, binding, null, _cancellationToken);

                    bindingsCount++;
                }
            }

            _logger.LogInformation("{BindingsCount} persistent bindings are loaded.", bindingsCount);
        }

        private async Task LoadMessagesAsync()
        {
            var messageCount = 0;
            var sw = Stopwatch.StartNew();

            foreach (var user in _storage.Users.Values)
            {
                foreach (var queue in user.Queues.Values)
                {
                    using var context = new ClientContext { User = user, Logger = _logger, Storage = _storage, Mode = ClientContextMode.Reinstate };

                    var messages = (await _storage.PersistentStorage.LoadMessageKeysAsync(queue.Entity.User, queue.Entity.Name, _cancellationToken)).ToList();
                    var queueMessageCount = 0;

                    foreach (var key in messages)
                    {
                        var message = await _storage.PersistentStorage.LoadMessageAsync(user.Entity.Username, queue.Entity.Name, key, false, _cancellationToken);

                        if (message != null)
                        {
                            var handler = new MessageHandler();
                            await handler.ExecuteEntityAsync(context, queue.Entity.Name, message, null, _cancellationToken);

                            messageCount++;
                        }

                        queueMessageCount++;

                        if (sw.Elapsed > TimeSpan.FromSeconds(10))
                        {
                            _logger.LogInformation("{MessageCount} persistent messages are loaded. {QueueMessageCount}/{QueueTotalMessageCount} loaded for queue '{Queue}'.",
                                messageCount, queueMessageCount, messages.Count, queue.Entity.Name);
                            sw.Restart();
                        }
                    }
                }
            }

            _logger.LogInformation("{MessageCount} persistent messages are loaded.", messageCount);
        }
    }
}
