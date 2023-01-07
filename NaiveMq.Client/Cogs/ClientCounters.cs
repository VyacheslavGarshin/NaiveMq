using System;

namespace NaiveMq.Client.Cogs
{
    /// <summary>
    /// Client counters.
    /// </summary>
    public class ClientCounters : IDisposable
    {
        /// <summary>
        /// Message read speed.
        /// </summary>
        public SpeedCounters Read { get; }

        /// <summary>
        /// Message write speed.
        /// </summary>
        public SpeedCounters Write { get; }

        /// <summary>
        /// Any command read speed.
        /// </summary>
        public SpeedCounters ReadCommand { get; }

        /// <summary>
        /// Any command write speed.
        /// </summary>
        public SpeedCounters WriteCommand { get; }

        private SpeedCounterService _service = new();

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClientCounters()
        {
            Read = new(_service);
            Write = new(_service);
            ReadCommand = new(_service);
            WriteCommand = new(_service);
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            _service.Dispose();
            Read.Dispose();
            Write.Dispose();
            ReadCommand.Dispose();
            WriteCommand.Dispose();
        }
    }
}
