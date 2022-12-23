using System;

namespace NaiveMq.Client.Common
{
    public class ClientCounters : IDisposable
    {
        public SpeedCounters Read { get; }

        public SpeedCounters Write { get; }

        public SpeedCounters ReadCommand { get; }

        public SpeedCounters WriteCommand { get; }

        private SpeedCounterService _service = new();

        public ClientCounters()
        {
            Read = new(_service);
            Write = new(_service);
            ReadCommand = new(_service);
            WriteCommand = new(_service);
        }

        public virtual void Dispose()
        {
            _service.Dispose();
            Read.Dispose();
            Write.Dispose();
            ReadCommand.Dispose();
            WriteCommand.Dispose();
        }
    }
}
