using System;

namespace NaiveMq.Client.Cogs
{
    /// <summary>
    /// Triade of speed counters.
    /// </summary>
    public class SpeedCounters : IDisposable
    {
        private SpeedCounterService _service;

        /// <summary>
        /// Second counter.
        /// </summary>
        public SpeedCounter Second { get; private set; }

        /// <summary>
        /// Minute counter.
        /// </summary>
        public SpeedCounter Minute { get; private set; }

        /// <summary>
        /// Hour counter.
        /// </summary>
        public SpeedCounter Hour { get; private set; }

        /// <summary>
        /// Parent counter.
        /// </summary>
        public SpeedCounters Parent { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="parent"></param>
        /// <param name="value"></param>
        /// <param name="average"></param>
        public SpeedCounters(SpeedCounterService service, SpeedCounters parent = null, long value = 0, bool average = false)
        {
            _service = service;
            Parent = parent;
            Second = _service.Create(Enums.CounterInterval.Second, value, average);
            Minute = _service.Create(Enums.CounterInterval.Minute, value, average);
            Hour = _service.Create(Enums.CounterInterval.Hour, value, average);
        }

        /// <summary>
        /// Add.
        /// </summary>
        /// <param name="value"></param>
        public void Add(long value = 1)
        {
            Second.Add(value);
            Minute.Add(value);
            Hour.Add(value);

            Parent?.Add(value);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _service.Delete(Second);
            _service.Delete(Minute);
            _service.Delete(Hour);
        }
    }
}
