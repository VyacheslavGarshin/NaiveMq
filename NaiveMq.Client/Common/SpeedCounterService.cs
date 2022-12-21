using NaiveMq.Client.Enums;
using System;
using System.Collections.Generic;
using System.Threading;

namespace NaiveMq.Client.Common
{
    public class SpeedCounterService : IDisposable
    {
        private readonly IntervalService _second = new IntervalService(CounterInterval.Second);

        private readonly IntervalService _minute = new IntervalService(CounterInterval.Minute);

        private readonly IntervalService _hour = new IntervalService(CounterInterval.Hour);

        public SpeedCounter Create(CounterInterval counterInterval, long value = 0)
        {
            switch (counterInterval)
            {
                case CounterInterval.Second:
                    return _second.Create(value);
                case CounterInterval.Minute:
                    return _minute.Create(value);
                case CounterInterval.Hour:
                    return _hour.Create(value);
                default:
                    throw new NotSupportedException($"Counter interval {counterInterval} is not supported.");
            }
        }

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
            
            private HashSet<SpeedCounter> _counters = new();

            public IntervalService(CounterInterval counterInterval)
            {
                _counterInterval = counterInterval;
                _timer = new Timer(Timer_OnTimer, null, TimeSpan.Zero, TimeSpan.FromSeconds((int)counterInterval));
            }

            public SpeedCounter Create(long value = 0)
            {
                var result = new SpeedCounter(_counterInterval, value);
                _counters.Add(result);
                return result;
            }

            public void Delete(SpeedCounter counter)
            {
                if (_counters != null && !_counters.Remove(counter))
                {
                    throw new ArgumentOutOfRangeException(nameof(counter));
                }
            }

            public void Dispose()
            {
                _timer.Dispose();
                _counters = null;
            }

            private void Timer_OnTimer(object state)
            {
                foreach (var counter in _counters)
                {
                    counter.OnTimer();
                }
            }
        }
    }
}
