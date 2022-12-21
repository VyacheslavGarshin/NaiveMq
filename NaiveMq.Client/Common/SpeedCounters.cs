using System;

namespace NaiveMq.Client.Common
{
    public class SpeedCounters : IDisposable
    {
        private SpeedCounterService _service;

        public SpeedCounter Second { get; private set; }

        public SpeedCounter Minute { get; private set; }

        public SpeedCounter Hour { get; private set; }

        public SpeedCounters Parent { get; set; }

        public SpeedCounters(SpeedCounterService service, SpeedCounters parent = null)
        {
            _service = service;
            Parent = parent;
            Second = _service.Create(Enums.CounterInterval.Second);
            Minute = _service.Create(Enums.CounterInterval.Minute);
            Hour = _service.Create(Enums.CounterInterval.Hour);
        }

        public void Add(long value = 1)
        {
            Second.Add(value);
            Minute.Add(value);
            Hour.Add(value);

            Parent?.Add(value);
        }

        public void Dispose()
        {
            _service.Delete(Second);
            _service.Delete(Minute);
            _service.Delete(Hour);
        }
    }
}
