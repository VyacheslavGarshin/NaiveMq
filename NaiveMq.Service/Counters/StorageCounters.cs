using NaiveMq.Client.Cogs;

namespace NaiveMq.Service.Counters
{
    public class StorageCounters : UserCounters
    {
        public StorageCounters(SpeedCounterService service) : base(service)
        {
        }

        public StorageCounters(SpeedCounterService service, ServiceCounters parent) : base(service)
        {
            Read.Parent = parent.Read;
            Write.Parent = parent.Write;
            Length.Parent = parent.Length;
            Volume.Parent = parent.Volume;
            VolumeInMemory.Parent = parent.VolumeInMemory;
            Subscriptions.Parent = parent.Subscriptions;
        }
    }
}
