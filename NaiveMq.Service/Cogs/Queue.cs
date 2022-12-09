using NaiveMq.Client.Enums;
using NaiveMq.Service.Entities;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class Queue : IDisposable
    {
        public string User { get; set; }

        public string Name { get; set; }

        public bool Durable { get; set; }

        public bool Exchange { get; set; }

        public int Length => _messages.Count;

        public long Volume => _volume;

        public long VolumeInMemory => _volumeInMemory;

        private SemaphoreSlim _dequeueSemaphore { get; set; } = new SemaphoreSlim(0, int.MaxValue);

        private long _volume;

        private long _volumeInMemory;

        private readonly ConcurrentQueue<MessageEntity> _messages = new();

        public Queue(string name, string user, bool durable, bool exchange)
        {
            Name = name;
            User = user;
            Durable = durable;
            Exchange = exchange;
        }

        public bool TryDequeue(out MessageEntity message)
        {
            var result = _messages.TryDequeue(out message);

            if (message != null)
            {
                Interlocked.Add(ref _volume, -message.DataLength);

                if (message.Persistent != Persistent.DiskOnly)
                {
                    Interlocked.Add(ref _volumeInMemory, -message.DataLength);
                }
            }

            return result;
        }

        public void Enqueue(MessageEntity message)
        {
            _messages.Enqueue(message);

            Interlocked.Add(ref _volume, message.DataLength);

            if (message.Persistent != Persistent.DiskOnly)
            {
                Interlocked.Add(ref _volumeInMemory, message.DataLength);
            }
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
            _volume = 0;
            _volumeInMemory = 0;
        }
    }
}
