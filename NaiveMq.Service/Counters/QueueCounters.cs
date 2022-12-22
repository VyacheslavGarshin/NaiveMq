using NaiveMq.Client.Common;

namespace NaiveMq.Service.Counters
{
    public class QueueCounters : IDisposable
    {
        public SpeedCounters Read { get; }

        public SpeedCounters Write { get; }

        public Counter Length { get; } = new();

        public Counter Volume { get; } = new();

        public Counter VolumeInMemory { get; } = new();

        public Counter Subscriptions { get; } = new();

        /// <summary>
        /// Average time beetween messages put to queue and ready to send.
        /// </summary>
        public AvgCounter AvgLifeTime { get; } = new(10);

        /// <summary>
        /// Average IO time during life time.
        /// </summary>
        public AvgCounter AvgIoTime { get; } = new(10);

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
            Subscriptions.Parent = parent.Subscriptions;
            AvgLifeTime.Parent = parent.AvgLifeTime;
            AvgIoTime.Parent = parent.AvgIoTime;
        }

        public virtual void Dispose()
        {
            Read.Dispose();
            Write.Dispose();
        }
    }
}
