using System;

namespace NaiveMq.Client.Commands
{
    public class Subscribe : AbstractRequest<Confirmation>
    {
        public string Queue { get; set; }

        /// <summary>
        /// Client should confirm message by sending Confirmation command back to server.
        /// </summary>
        public bool ClientConfirm { get; set; } = true;

        /// <summary>
        /// Message will be returned to the queue if no confirmation is received by server.
        /// </summary>
        /// <remarks>If not set then server default will be used.</remarks>
        public TimeSpan? ClientConfirmTimeout { get; set; }
    }
}
