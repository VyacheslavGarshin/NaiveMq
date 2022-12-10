using NaiveMq.Client.Enums;
using NaiveMq.Service.Entities;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class QueueCog : IDisposable
    {
        public QueueEntity Entity { get; set; }

        public long? LengthLimit { get; set; }

        public int Length => _messages.Count;

        public long Volume => _volume;

        public long VolumeInMemory => _volumeInMemory;

        private SemaphoreSlim _dequeueSemaphore { get; set; } = new SemaphoreSlim(0, int.MaxValue);

        private SemaphoreSlim _limitSemaphore { get; set; } = new SemaphoreSlim(0, 1);

        private long _volume;

        private long _volumeInMemory;

        private readonly ConcurrentQueue<MessageEntity> _messages = new();

        public QueueCog(QueueEntity entity)
        {
            Entity = entity;
        }

        public async Task<MessageEntity> TryDequeue(CancellationToken cancellationToken)
        {
            await _dequeueSemaphore.WaitAsync(cancellationToken);

            if (_messages.TryDequeue(out var message))
            {
                Interlocked.Add(ref _volume, -message.DataLength);

                if (message.Persistent != Persistent.DiskOnly)
                {
                    Interlocked.Add(ref _volumeInMemory, -message.DataLength);
                }
            }

            try
            {
                if (_limitSemaphore.CurrentCount == 0 && !LimitExceeded(message))
                {
                    LengthLimit = null;

                    _limitSemaphore.Release();
                }
            }
            catch (SemaphoreFullException)
            {

            }

            return message;
        }

        public void Enqueue(MessageEntity message)
        {
            _messages.Enqueue(message);

            Interlocked.Add(ref _volume, message.DataLength);

            if (message.Persistent != Persistent.DiskOnly)
            {
                Interlocked.Add(ref _volumeInMemory, message.DataLength);
            }

            _dequeueSemaphore.Release();
        }

        public async Task<bool> WaitLimitSemaphore(TimeSpan timout, CancellationToken cancellationToken)
        {
            return await _limitSemaphore.WaitAsync((int)timout.TotalMilliseconds, cancellationToken);
        }

        public bool LimitExceeded(MessageEntity message)
        {
            return
                Entity.Limit != null && (
                    Length >= Entity.Limit && Entity.LimitBy == LimitBy.Length ||
                    Volume + message.DataLength >= Entity.Limit && Entity.LimitBy == LimitBy.Volume) ||
                (LengthLimit != null && Length >= LengthLimit);
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
