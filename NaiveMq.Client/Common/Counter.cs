using System.Threading;

namespace NaiveMq.Client.Common
{
    public class Counter
    {
        public long Value => _value;

        public Counter Parent { get; set; }

        private long _value;

        public Counter()
        {
        }

        public Counter(Counter parent)
        {
            Parent = parent;
        }

        public void Add(long value = 1)
        {
            Interlocked.Add(ref _value, value);
            Parent?.Add(value);
        }

        public void Reset(long value = 0)
        {
            var prev = Interlocked.Exchange(ref _value, value);
            Parent?.Add(-prev + value);
        }
    }
}
