using NaiveMq.Client.Entities;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class Queue : IDisposable
    {
        public string User { get; set; }

        public string Name { get; set; }

        public bool Durable { get; set; }

        public bool Exchange { get; set; }

        private SemaphoreSlim _dequeueSemaphore { get; set; } = new SemaphoreSlim(0, int.MaxValue);

        private readonly ConcurrentQueue<MessageEntity> _messages = new();

        public Queue(string name, string user, bool durable, bool exchange)
        {
            Name = name;
            User = user;
            Durable = durable;
            Exchange = exchange;
        }

        public bool TryDequeue(out MessageEntity enqueue)
        {
            return _messages.TryDequeue(out enqueue);
        }

        public void Enqueue(MessageEntity enqueue)
        {
            _messages.Enqueue(enqueue);
        }

        public async Task WaitDequeueAsync(CancellationToken cancellationToken)
        {
            await _dequeueSemaphore.WaitAsync(cancellationToken);
        }

        public void ReleaseDequeue()
        {
            _dequeueSemaphore.Release();
        }

        public void Dispose()
        {
            _dequeueSemaphore.Dispose();
            _messages.Clear();
        }
    }
}
