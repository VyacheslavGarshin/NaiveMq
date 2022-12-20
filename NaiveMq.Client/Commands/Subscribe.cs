using NaiveMq.Client.Enums;
using System;

namespace NaiveMq.Client.Commands
{
    public class Subscribe : AbstractRequest<Confirmation>
    {
        public string Queue { get; set; }

        /// <summary>
        /// Subscriber should confirm message by sending Confirmation command back to server.
        /// </summary>
        public bool ConfirmMessage { get; set; } = true;

        /// <summary>
        /// Message will be returned to the queue if no confirmation is received by server in case <see cref="ConfirmMessage"/> is set.
        /// </summary>
        /// <remarks>If not set then server default will be used.</remarks>
        public TimeSpan? ConfirmMessageTimeout { get; set; }

        /// <summary>
        /// Behaviour when the current server queue runs out of messages.
        /// </summary>
        public ClusterStrategy ClusterStrategy { get; set; }

        public Subscribe()
        {
        }

        public Subscribe(string queue, bool confirmMessage = true, TimeSpan? confirmMessageTimeout = null, ClusterStrategy clusterStrategy = ClusterStrategy.Proxy)
        {
            Queue = queue;
            ConfirmMessage = confirmMessage;
            ConfirmMessageTimeout = confirmMessageTimeout;
            ClusterStrategy = clusterStrategy;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Queue))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Queue) });
            }
        }
    }
}
