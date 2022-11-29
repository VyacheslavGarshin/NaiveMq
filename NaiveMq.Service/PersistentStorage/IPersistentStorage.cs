using NaiveMq.Client.Entities;

namespace NaiveMq.Service.PersistentStorage
{
    public interface IPersistentStorage : IDisposable
    {
        public Task SaveUserAsync(UserEntity user, CancellationToken cancellationToken);

        public Task DeleteUserAsync(string user, CancellationToken cancellationToken);

        public Task<UserEntity> LoadUserAsync(string user, CancellationToken cancellationToken);

        public Task<IEnumerable<string>> LoadUserKeysAsync(CancellationToken cancellationToken);

        public Task SaveQueueAsync(string user, QueueEntity queue, CancellationToken cancellationToken);

        public Task DeleteQueueAsync(string user, string queue, CancellationToken cancellationToken);

        public Task<QueueEntity> LoadQueueAsync(string user, string queue, CancellationToken cancellationToken);

        public Task<IEnumerable<string>> LoadQueueKeysAsync(string user, CancellationToken cancellationToken);

        public Task SaveBindingAsync(string user, BindingEntity binding, CancellationToken cancellationToken);

        public Task DeleteBindingAsync(string user, string exchange, string queue, CancellationToken cancellationToken);

        public Task<BindingEntity> LoadBindingAsync(string user, string exchange, string queue, CancellationToken cancellationToken);

        public Task<IEnumerable<BindingKey>> LoadBindingKeysAsync(string user, CancellationToken cancellationToken);

        public Task SaveMessageAsync(string user, string queue, MessageEntity message, CancellationToken cancellationToken);

        public Task DeleteMessageAsync(string user, string queue, Guid messageId, CancellationToken cancellationToken);

        public Task<MessageEntity> LoadMessageAsync(string user, string queue, Guid messageId, CancellationToken cancellationToken);

        public Task<IEnumerable<Guid>> LoadMessageKeysAsync(string user, string queue, CancellationToken cancellationToken);
    }
}