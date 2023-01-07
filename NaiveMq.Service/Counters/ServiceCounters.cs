using NaiveMq.Client.Cogs;

namespace NaiveMq.Service.Counters
{
    public class ServiceCounters : UserCounters
    {
        public SpeedCounters ReadCommand { get; }

        public SpeedCounters WriteCommand { get; }

        public ServiceCounters(SpeedCounterService service) : base(service)
        {
            ReadCommand = new(service);
            WriteCommand = new(service);
        }

        public override void Dispose()
        {
            base.Dispose();

            ReadCommand.Dispose();
            WriteCommand.Dispose();
        }
    }
}
