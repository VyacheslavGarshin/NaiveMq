using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace NaiveMq.Client.Common
{
    public class SpeedCounter : IDisposable
    {
        public int StoreCount { get; }

        public long LastResult;

        public long Total;

        public ReadOnlyCollection<long> Results => _results.AsReadOnly();

        public List<long> _results { get; } = new List<long>();

        private readonly Timer _timer;

        private long _currentCounter;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storeCount"></param>
        /// <param name="interval">Default is one second.</param>
        public SpeedCounter(int storeCount = 10, TimeSpan? interval = null)
        {
            _timer = new Timer(OnTimer, null, TimeSpan.Zero, interval ?? TimeSpan.FromSeconds(1));
            StoreCount = storeCount;
        }


        public void Add(long amount = 1)
        {
            Interlocked.Add(ref _currentCounter, amount);

            try
            {
                if (Total < long.MaxValue)
                    Interlocked.Add(ref Total, 1);
            }
            catch (Exception)
            {
                // no lock for speed
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

            if (_results.Count > StoreCount)
            {
                _results.RemoveAt(0);
            }
        }
    }
}
