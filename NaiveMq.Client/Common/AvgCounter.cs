namespace NaiveMq.Client.Common
{
    public class AvgCounter
    {
        public long? Value { get; private set; }

        public AvgCounter Parent { get; set; }

        private long?[] _cases;

        private long _spin;

        private long _count;

        public AvgCounter(long count = 3, long? value = null)
        {
            _count = count;
            _spin = _count;
            _cases = new long?[_count];

            if (value != null)
            {
                Add(value.Value);
            }
        }

        public AvgCounter(AvgCounter parent, long count = 3, long? value = null) : this(count, value)
        {
            Parent = parent;
        }

        public void Add(long value = 1)
        {
            long? avg = null;

            var index = _spin % _count;
            _cases[index] = value;

            if (_spin < long.MaxValue - 1000)
            {
                _spin++;
            }
            else
            {
                _spin = _count;
            }

            var notNullCases = 0;

            for (var i = 0; i < _count; i++)
            {
                var v = _cases[i];

                if (v != null)
                {
                    avg ??= 0;
                    avg += v;
                    notNullCases++;
                }
            }

            if (avg != null)
            {
                avg /= notNullCases;
            }

            Value = avg;

            if (avg != null)
            {
                Parent?.Add(avg.Value);
            }
        }

        public void Reset(long value = 0)
        {
            for (var i = 0; i < _count; i++)
            {
                _cases[i] = null;
            }

            Add(value);
        }
    }
}
