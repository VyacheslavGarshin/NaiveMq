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

        public Counter Subscriptions { get; set; } = new();

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
        }

        public virtual void Dispose()
        {
            Read.Dispose();
            Write.Dispose();
        }
    }
}
