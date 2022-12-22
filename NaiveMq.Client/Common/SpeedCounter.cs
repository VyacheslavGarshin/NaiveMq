using NaiveMq.Client.Enums;

namespace NaiveMq.Client.Common
{
    public class SpeedCounter
    {
        public CounterInterval CounterInterval { get; }

        public long Value { get; private set; }

        public bool Average { get; private set; }

        private Counter _counting = new();

        public SpeedCounter()
        {
        }

        public SpeedCounter(CounterInterval counterInterval, bool average) : this()
        {
            CounterInterval = counterInterval;
            Average = average;  
        }

        public SpeedCounter(CounterInterval counterInterval, long value, bool average) : this(counterInterval, average)
        {
            Value = value;
        }

        public void Add(long value = 1)
        {
            _counting.Add(value);
        }

        public void OnTimer()
        {
            Value = Average && _counting.Count != 0 ? _counting.Value / _counting.Count : _counting.Value;
            _counting.Reset();
        }
    }
}
