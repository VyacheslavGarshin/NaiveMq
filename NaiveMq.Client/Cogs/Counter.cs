using System.Threading;

namespace NaiveMq.Client.Cogs
{
    /// <summary>
    /// Simple thread safe counter.
    /// </summary>
    public class Counter
    {
        /// <summary>
        /// Value.
        /// </summary>
        public long Value => _value;

        /// <summary>
        /// Number of times <see cref="Add"/> function was called.
        /// </summary>
        public long Count => _count;

        /// <summary>
        /// Parent counter where counting will be propagated.
        /// </summary>
        public Counter Parent { get; set; }

        private long _value;

        private long _count;

        /// <summary>
        /// Constructor.
        /// </summary>
        public Counter()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="parent"></param>
        public Counter(Counter parent)
        {
            Parent = parent;
        }

        /// <summary>
        /// Add value.
        /// </summary>
        /// <param name="value"></param>
        public void Add(long value = 1)
        {
            Interlocked.Add(ref _value, value);
            Interlocked.Increment(ref _count);
            Parent?.Add(value);
        }

        /// <summary>
        /// Reset to value.
        /// </summary>
        /// <param name="value"></param>
        public void Reset(long value = 0)
        {
            var prev = Interlocked.Exchange(ref _value, value);
            Interlocked.Exchange(ref _count, 0);
            Parent?.Add(-prev + value);
        }
    }
}
