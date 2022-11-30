using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace NaiveMq.Client.Common
{
    public class SpeedCounter : IDisposable
    {
        public int ResultCount { get; private set; }

        public long LastResult { get; private set; }

        public long Total => _total;

        public ReadOnlyCollection<long> Results => _results.AsReadOnly();

        public List<long> _results { get; } = new List<long>();

        private readonly Timer _timer;

        private long _currentCounter;

        private long _total;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resultCount"></param>
        /// <param name="interval">Default is one second.</param>
        public SpeedCounter(int resultCount = 10, TimeSpan? interval = null)
        {
            _timer = new Timer(OnTimer, null, TimeSpan.Zero, interval ?? TimeSpan.FromSeconds(1));
            ResultCount = resultCount;
        }

        public void Add(long amount = 1)
        {
            Interlocked.Add(ref _currentCounter, amount);

            try
            {
                Interlocked.Add(ref _total, 1);
            }
            catch (Exception)
            {
                _total = 0;
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private void OnTimer(object state)
        {
            LastResult = _currentCounter;
            _currentCounter = 0;

            _results.Add(LastResult);

            if (_results.Count > ResultCount)
            {
                _results.RemoveAt(0);
            }
        }
    }
}
