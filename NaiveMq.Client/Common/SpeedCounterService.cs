﻿using NaiveMq.Client.Enums;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace NaiveMq.Client.Common
{
    public class SpeedCounterService : IDisposable
    {
        private readonly IntervalService _second = new IntervalService(CounterInterval.Second);

        private readonly IntervalService _minute = new IntervalService(CounterInterval.Minute);

        private readonly IntervalService _hour = new IntervalService(CounterInterval.Hour);

        public SpeedCounter Create(CounterInterval counterInterval, long value = 0, bool average = false)
        {
            switch (counterInterval)
            {
                case CounterInterval.Second:
                    return _second.Create(value, average);
                case CounterInterval.Minute:
                    return _minute.Create(value, average);
                case CounterInterval.Hour:
                    return _hour.Create(value, average);
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
            
            private ConcurrentDictionary<int, SpeedCounter> _counters = new();

            public IntervalService(CounterInterval counterInterval)
            {
                _counterInterval = counterInterval;
                _timer = new Timer(Timer_OnTimer, null, TimeSpan.Zero, TimeSpan.FromSeconds((int)counterInterval));
            }

            public SpeedCounter Create(long value = 0, bool average = false)
            {
                var result = new SpeedCounter(_counterInterval, value, average);
                _counters.TryAdd(result.GetHashCode(),result);
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
