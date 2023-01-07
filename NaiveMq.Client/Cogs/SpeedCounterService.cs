using NaiveMq.Client.Enums;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace NaiveMq.Client.Cogs
{
    /// <summary>
    /// Speed counter service.
    /// </summary>
    public class SpeedCounterService : IDisposable
    {
        private readonly IntervalService _second = new(CounterInterval.Second);

        private readonly IntervalService _minute = new(CounterInterval.Minute);

        private readonly IntervalService _hour = new(CounterInterval.Hour);

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="counterInterval"></param>
        /// <param name="value"></param>
        /// <param name="average"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public SpeedCounter Create(CounterInterval counterInterval, long value = 0, bool average = false)
        {
            return counterInterval switch
            {
                CounterInterval.Second => _second.Create(value, average),
                CounterInterval.Minute => _minute.Create(value, average),
                CounterInterval.Hour => _hour.Create(value, average),
                _ => throw new NotSupportedException($"Counter interval {counterInterval} is not supported."),
            };
        }

        /// <summary>
        /// Delete counter from service.
        /// </summary>
        /// <param name="counter"></param>
        /// <exception cref="NotSupportedException"></exception>
        public void Delete(SpeedCounter counter)
        {
            switch (counter.CounterInterval)
            {
                case CounterInterval.Second:
                    _second.Delete(counter);
                    break;
                case CounterInterval.Minute:
                    _minute.Delete(counter);
                    break;
                case CounterInterval.Hour:
                    _hour.Delete(counter);
                    break;
                default:
                    throw new NotSupportedException($"Counter interval {counter.CounterInterval} is not supported.");
            }
        }


        /// <inheritdoc/>
        public void Dispose()
        {
            _second.Dispose();
            _minute.Dispose();
            _hour.Dispose();
        }

        private class IntervalService : IDisposable
        {
            private readonly Timer _timer;

            private readonly CounterInterval _counterInterval;

            private readonly ConcurrentDictionary<int, SpeedCounter> _counters = new();

            public IntervalService(CounterInterval counterInterval)
            {
                _counterInterval = counterInterval;
                _timer = new Timer(Timer_OnTimer, null, TimeSpan.Zero, TimeSpan.FromSeconds((int)counterInterval));
            }

            public SpeedCounter Create(long value = 0, bool average = false)
            {
                var result = new SpeedCounter(_counterInterval, value, average);
                _counters.TryAdd(result.GetHashCode(), result);
                return result;
            }

            public void Delete(SpeedCounter counter)
            {
                _counters.TryRemove(counter.GetHashCode(), out var _);
            }

            public void Dispose()
            {
                _timer.Dispose();
                _counters.Clear();
            }

            private void Timer_OnTimer(object state)
            {
                foreach (var counter in _counters.Values)
                {
                    counter.OnTimer();
                }
            }
        }
    }
}
