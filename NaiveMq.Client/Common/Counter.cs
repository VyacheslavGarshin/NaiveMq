using System.Threading;

namespace NaiveMq.Client.Common
{
    public class Counter
    {
        public long Value => _value;

        /// <summary>
        /// Number of times <see cref="Add"/> function was called.
        /// </summary>
        public long Count => _count;

        public Counter Parent { get; set; }

        private long _value;

        private long _count;

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
            Interlocked.Increment(ref _count);
            Parent?.Add(value);
        }

        public void Reset(long value = 0)
        {
            var prev = Interlocked.Exchange(ref _value, value);
            Interlocked.Exchange(ref _count, 0);
            Parent?.Add(-prev + value);
        }
    }
}
