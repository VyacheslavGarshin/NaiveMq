using System.Collections.Concurrent;
using System.Diagnostics;

namespace NaiveMq.Service.Cogs
{
    public class TimerService : IDisposable
    {
        public TimeSpan ActionsTime { get; private set; }

        private Timer _timer;

        private TimeSpan _interval;

        private ConcurrentDictionary<object, Action> _actions = new();

        public TimerService(TimeSpan interval)
        {
            _interval = interval;
            _timer = new Timer(OnTimer, null, TimeSpan.Zero, _interval);
        }

        public void Add(object obj, Action action)
        {
            if (!_actions.TryAdd(obj, action))
            {
                throw new ArgumentException("Object already registered the action.");
            }
        }

        public void Remove(object obj)
        {
            _actions.TryRemove(obj, out var _);
        }

        public void Dispose()
        {
            _timer.Dispose();
            _actions.Clear();
        }

        private void OnTimer(object state)
        {
            var sw = Stopwatch.StartNew();

            foreach (var action in _actions.Values)
            {
                action();
            }

            ActionsTime = sw.Elapsed;
        }
    }
}
