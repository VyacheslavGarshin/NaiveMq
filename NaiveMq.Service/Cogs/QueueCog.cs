using NaiveMq.Client;
using NaiveMq.Client.Common;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Counters;
using NaiveMq.Service.Entities;
using NaiveMq.Service.Enums;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class QueueCog : IDisposable
    {
        public QueueEntity Entity { get; set; }

        public QueueStatus Status { get; set; }

        public long? ForcedLengthLimit { get; set; }

        public long? VolumeLimit { get; set; }

        public int Length => _messages.Count;

        public QueueCounters Counters { get; }

        private SemaphoreSlim _dequeueSemaphore { get; set; }

        private SemaphoreSlim _limitSemaphore { get; set; }
        
        private readonly ConcurrentQueue<MessageEntity> _messages = new();

        public QueueCog(QueueEntity entity, UserCounters userCounters, SpeedCounterService speedCounterService)
        {
            Entity = entity;
            CreateSemaphores();
            Counters = new(speedCounterService, userCounters);
        }

        public async Task<MessageEntity> TryDequeueAsync(CancellationToken cancellationToken)
        {
            CheckStarted();

            try
            {
                await _dequeueSemaphore.WaitAsync(cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                throw new ServerException(ErrorCode.QueueStopped);
            }

            if (_messages.TryDequeue(out var message))
            {
                Counters.Length.Add(-1);
                Counters.Volume.Add(-message.DataLength);
                if (message.Persistent != Persistence.DiskOnly)
                {
                    Counters.VolumeInMemory.Add(-message.DataLength);
                }
                Counters.Write.Add();
            }

            try
            {
                if (_limitSemaphore.CurrentCount == 0 && LimitExceeded(message.DataLength) == LimitType.None)
                {
                    ForcedLengthLimit = null;
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
            CheckStarted();

            _messages.Enqueue(message);

            _dequeueSemaphore.Release();

            Counters.Length.Add();
            Counters.Volume.Add(message.DataLength);
            if (message.Persistent != Persistence.DiskOnly)
            {
                Counters.VolumeInMemory.Add(message.DataLength);
            }
            Counters.Read.Add();
        }

        public async Task<bool> WaitLimitSemaphoreAsync(TimeSpan timout, CancellationToken cancellationToken)
        {
            return await _limitSemaphore.WaitAsync((int)timout.TotalMilliseconds, cancellationToken);
        }

        public LimitType LimitExceeded(int dataLength)
        {
            if ((Entity.LengthLimit != null && (Length >= Entity.LengthLimit)) || (ForcedLengthLimit != null && (Length >= ForcedLengthLimit)))
            {
                return LimitType.Length;
            }

            if (Entity.VolumeLimit != null && (Counters.Volume.Value + dataLength >= Entity.VolumeLimit))
            {
                return LimitType.Volume;
            }

            return LimitType.None;
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
            Counters.Dispose();
        }

        private void ClearData()
        {
            _messages.Clear();
            Counters.Volume.Reset();
            Counters.VolumeInMemory.Reset();
        }

        private void CheckStarted()
        {
            if (Status != QueueStatus.Started)
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
