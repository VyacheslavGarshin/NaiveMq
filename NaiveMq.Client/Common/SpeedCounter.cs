using NaiveMq.Client.Enums;

namespace NaiveMq.Client.Common
{
    public class SpeedCounter
    {
        public CounterInterval CounterInterval { get; }

        public long Value { get; private set; }

        private Counter _counting = new();

        public SpeedCounter()
        {
        }

        public SpeedCounter(CounterInterval counterInterval) : this()
        {
            CounterInterval = counterInterval;
        }

        public SpeedCounter(CounterInterval counterInterval, long value) : this(counterInterval)
        {
            Value = value;
        }

        public void Add(long value = 1)
        {
            _counting.Add(value);
        }

        public void OnTimer()
        {
            Value = _counting.Value;
            _counting.Reset();
        }
    }
}
