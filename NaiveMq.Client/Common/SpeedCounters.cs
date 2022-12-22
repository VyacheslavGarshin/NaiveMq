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

        public SpeedCounters(SpeedCounterService service, SpeedCounters parent = null, long value = 0, bool average = false)
        {
            _service = service;
            Parent = parent;
            Second = _service.Create(Enums.CounterInterval.Second, value, average);
            Minute = _service.Create(Enums.CounterInterval.Minute, value, average);
            Hour = _service.Create(Enums.CounterInterval.Hour, value, average);
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
