using NaiveMq.Client;
using NaiveMq.Client.Common;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Entities;
using NaiveMq.Service.Enums;
using System.Collections.Concurrent;
using static NaiveMq.Service.Cogs.UserCog;

namespace NaiveMq.Service.Cogs
{
    public class QueueCog : IDisposable
    {
        public QueueEntity Entity { get; set; }

        public bool Started { get; set; } = true;

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

            await _dequeueSemaphore.WaitAsync(cancellationToken);

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

        public class QueueCounters : IDisposable
        {
            public SpeedCounters Read { get; }

            public SpeedCounters Write { get; }

            public Counter Length { get; } = new();

            public Counter Volume { get; } = new();

            public Counter VolumeInMemory { get; } = new();

            public QueueCounters(SpeedCounterService service)
            {
                Read = new(service);
                Write = new(service);
            }

            public QueueCounters(SpeedCounterService service, UserCounters parent) : this(service)
            {
                Read.Parent = parent.Read;
                Write.Parent = parent.Write;
                Length.Parent = parent.Length;
                Volume.Parent = parent.Volume;
                VolumeInMemory.Parent = parent.VolumeInMemory;
            }

            public virtual void Dispose()
            {
                Read.Dispose();
                Write.Dispose();
            }
        }
    }
}
