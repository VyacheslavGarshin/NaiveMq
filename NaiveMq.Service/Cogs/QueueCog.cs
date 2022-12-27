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
        public UserCog User { get; set; }

        public QueueEntity Entity { get; set; }

        public QueueStatus Status { get; private set; }

        public long? ForcedLengthLimit { get; set; }

        public long? VolumeLimit { get; set; }

        public int Length => _messages.Count;

        public QueueCounters Counters { get; }

        private static readonly QueueStatus[] _constantStatuses = new QueueStatus[] { QueueStatus.Started, QueueStatus.Deleted };

        private SemaphoreSlim _dequeueSemaphore { get; set; }

        private SemaphoreSlim _limitSemaphore { get; set; }
        
        private readonly ConcurrentQueue<MessageEntity> _messages = new();

        private readonly object _locker = new object();

        public QueueCog(QueueEntity entity, UserCog userCog, SpeedCounterService speedCounterService)
        {
            Entity = entity;
            CreateSemaphores();
            Counters = new(speedCounterService, userCog.Counters);
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
                throw new ServerException(ErrorCode.QueueNotStarted);
            }

            if (_messages.TryDequeue(out var message))
            {
                DecrementCounters(message);
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
            
            IncrementCounters(message);
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

        public void SetStatus(QueueStatus status)
        {
            lock (_locker)
            {
                if (_constantStatuses.Contains(status) || Status == QueueStatus.Started)
                {
                    Status = status;
                }
                else
                {
                    throw new ServerException(ErrorCode.QueueNotStarted);
                }
            }
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
            User = null;
        }

        private void ClearData()
        {
            _messages.Clear();
            Counters.Length.Reset();
            Counters.Volume.Reset();
            Counters.VolumeInMemory.Reset();
        }

        private void CheckStarted()
        {
            if (Status != QueueStatus.Started)
            {
                throw new ServerException(ErrorCode.QueueNotStarted);
            }
        }

        private void IncrementCounters(MessageEntity message)
        {
            Counters.Length.Add();
            Counters.Volume.Add(message.DataLength);
            Counters.Read.Add();

            if (message.Persistent != Persistence.DiskOnly)
            {
                Counters.VolumeInMemory.Add(message.DataLength);
            }
        }

        private void DecrementCounters(MessageEntity message)
        {
            Counters.Length.Add(-1);
            Counters.Volume.Add(-message.DataLength);
            Counters.Write.Add();

            if (message.Persistent != Persistence.DiskOnly)
            {
                Counters.VolumeInMemory.Add(-message.DataLength);
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
