using NaiveMq.Client.Common;

namespace NaiveMq.Service.Counters
{
    public class UserCounters : QueueCounters
    {
        public UserCounters(SpeedCounterService service) : base(service)
        {
        }

        public UserCounters(SpeedCounterService service, StorageCounters parent) : base(service)
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
    }
}
