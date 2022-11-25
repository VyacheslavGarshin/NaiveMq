using NaiveMq.Client.Entities;

namespace NaiveMq.Service.PersistentStorage
{
    public interface IPersistentStorage : IDisposable
    {
        public Task SaveUserAsync(UserEntity user, CancellationToken cancellationToken);

        public Task DeleteUserAsync(string user, CancellationToken cancellationToken);

        public Task<IEnumerable<UserEntity>> LoadUsersAsync(CancellationToken cancellationToken);

        public Task SaveQueueAsync(string user, QueueEntity queue, CancellationToken cancellationToken);

        public Task DeleteQueueAsync(string user, string queue, CancellationToken cancellationToken);

        public Task<IEnumerable<QueueEntity>> LoadQueuesAsync(string user, CancellationToken cancellationToken);

        public Task SaveMessageAsync(string user, string queue, MessageEntity message, CancellationToken cancellationToken);

        public Task DeleteMessageAsync(string user, string queue, Guid messageId, CancellationToken cancellationToken);

        public Task<IEnumerable<MessageEntity>> LoadMessagesAsync(string user, string queue, CancellationToken cancellationToken);
    }
}