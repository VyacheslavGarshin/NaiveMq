using System.Threading;

namespace NaiveMq.Client.Common
{
    public class Counter
    {
        public long Value => _value;

        private long _value;

        public void Add(long value = 1)
        {
            Interlocked.Add(ref _value, value);
        }

        public void Reset(long value = 0)
        {
            Interlocked.Exchange(ref _value, value);
        }
    }
}
