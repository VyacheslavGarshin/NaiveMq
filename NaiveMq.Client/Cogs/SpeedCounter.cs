using NaiveMq.Client.Enums;

namespace NaiveMq.Client.Cogs
{
    /// <summary>
    /// Counter reseting every given interval to accumulate results by second/hour etc.
    /// </summary>
    public class SpeedCounter
    {
        /// <summary>
        /// Interval.
        /// </summary>
        public CounterInterval CounterInterval { get; }

        /// <summary>
        /// Current value.
        /// </summary>
        public long Value { get; private set; }

        /// <summary>
        /// Mark counter as average. Than value will be the value / number of counts.
        /// </summary>
        public bool Average { get; private set; }

        private Counter _counting = new();

        /// <summary>
        /// Constructor.
        /// </summary>
        public SpeedCounter()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="counterInterval"></param>
        /// <param name="average"></param>
        public SpeedCounter(CounterInterval counterInterval, bool average) : this()
        {
            CounterInterval = counterInterval;
            Average = average;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="counterInterval"></param>
        /// <param name="value"></param>
        /// <param name="average"></param>
        public SpeedCounter(CounterInterval counterInterval, long value, bool average) : this(counterInterval, average)
        {
            Value = value;
        }

        /// <summary>
        /// Add.
        /// </summary>
        /// <param name="value"></param>
        public void Add(long value = 1)
        {
            _counting.Add(value);
        }

        internal void OnTimer()
        {
            Value = Average && _counting.Count != 0 ? _counting.Value / _counting.Count : _counting.Value;
            _counting.Reset();
        }
    }
}
