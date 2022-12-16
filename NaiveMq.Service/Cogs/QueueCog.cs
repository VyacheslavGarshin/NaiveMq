using NaiveMq.Client;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Entities;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class QueueCog : IDisposable
    {
        public QueueEntity Entity { get; set; }

        public bool Started { get; set; } = true;

        public long? LengthLimit { get; set; }

        public int Length => _messages.Count;

        public long Volume => _volume;

        public long VolumeInMemory => _volumeInMemory;

        private SemaphoreSlim _dequeueSemaphore { get; set; }

        private SemaphoreSlim _limitSemaphore { get; set; }

        private long _volume;

        private long _volumeInMemory;

        private readonly ConcurrentQueue<MessageEntity> _messages = new();

        public QueueCog(QueueEntity entity)
        {
            Entity = entity;
            CreateSemaphores();
        }

        public async Task<MessageEntity> TryDequeueAsync(CancellationToken cancellationToken)
        {
            CheckStarted();

            await _dequeueSemaphore.WaitAsync(cancellationToken);

            if (_messages.TryDequeue(out var message))
            {
                Interlocked.Add(ref _volume, -message.DataLength);

                if (message.Persistent != Persistence.DiskOnly)
                {
                    Interlocked.Add(ref _volumeInMemory, -message.DataLength);
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
            }

            return message;
        }

        public void Enqueue(MessageEntity message)
        {
            CheckStarted();

            _messages.Enqueue(message);

            Interlocked.Add(ref _volume, message.DataLength);

            if (message.Persistent != Persistence.DiskOnly)
            {
                Interlocked.Add(ref _volumeInMemory, message.DataLength);
            }

            _dequeueSemaphore.Release();
        }

        public async Task<bool> WaitLimitSemaphoreAsync(TimeSpan timout, CancellationToken cancellationToken)
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

        public void Clear()
        {
            DisposeSemaphores();
            ClearData();
            CreateSemaphores();
        }

        public void Dispose()
        {
            DisposeSemaphores();
            ClearData();
        }

        private void ClearData()
        {
            _messages.Clear();
            _volume = 0;
            _volumeInMemory = 0;
        }

        private void CheckStarted()
        {
            if (!Started)
            {
                throw new ServerException(ErrorCode.QueueStopped);
            }
        }

        private void CreateSemaphores()
        {
            _dequeueSemaphore = new SemaphoreSlim(0, int.MaxValue);
            _limitSemaphore = new SemaphoreSlim(0, 1);
        }

        private void DisposeSemaphores()
        {
            _dequeueSemaphore.Dispose();
            _limitSemaphore.Dispose();
        }
    }
}
