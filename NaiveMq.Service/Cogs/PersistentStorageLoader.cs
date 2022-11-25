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

                    var users = await LoadUsers();
                    var allQueues = new Dictionary<string, IEnumerable<QueueEntity>>();
                    await LoadQueues(users, allQueues);
                    await LoadMessagesAsync(allQueues);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading persistent data.");
                
                throw;
            }
        }

        private Task<IEnumerable<string>> LoadUsers()
        {
            return Task.FromResult(new[] { "guest" }.AsEnumerable());
        }

        private async Task LoadQueues(IEnumerable<string> users, Dictionary<string, IEnumerable<QueueEntity>> allQueues)
        {
            var queuesCount = 0;

            foreach (var user in users)
            {
                var queues = (await _storage.PersistentStorage.LoadQueues(user, _cancellationToken)).ToList();

                allQueues[user] = queues;

                var context = new HandlerContext { User = user, Logger = _logger, Storage = _storage, Reinstate = true, CancellationToken = _cancellationToken };

                foreach (var queue in queues)
                {
                    await new AddQueueHandler().ExecuteAsync(context, new AddQueue { Name = queue.Name, Durable = queue.Durable });
                    queuesCount++;
                }
            }

            _logger.LogInformation($"{queuesCount} queues are loaded.");
        }

        private async Task LoadMessagesAsync(Dictionary<string, IEnumerable<QueueEntity>> allQueues)
        {
            var messageCount = 0;

            foreach (var pair in allQueues)
            {
                var context = new HandlerContext { User = pair.Key, Logger = _logger, Storage = _storage, Reinstate = true, CancellationToken = _cancellationToken };

                foreach (var queue in pair.Value)
                {
                    var messages = await _storage.PersistentStorage.LoadMessages(queue.User, queue.Name, _cancellationToken);

                    foreach (var message in messages)
                    {
                        await new MessageHandler().ExecuteAsync(context, new Message { Id = message.Id, Queue = message.Queue, Text = message.Text });
                        messageCount++;
                    }
                }
            }

            _logger.LogInformation($"Loading of {messageCount} persistent messages is completed.");
        }
    }
}
