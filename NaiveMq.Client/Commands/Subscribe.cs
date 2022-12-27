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

        /// <summary>
        /// When there is no message sent for this time then <see cref="ClusterStrategy"/> is applied.
        /// </summary>
        /// <remarks>Default is 10 seconds.</remarks>
        public TimeSpan ClusterIdleTimout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Administrator can subscribe to any user.
        /// </summary>
        public string User { get; set; }

        public Subscribe()
        {
        }

        public Subscribe(
            string queue, 
            bool confirmMessage = true, 
            TimeSpan? confirmMessageTimeout = null, 
            ClusterStrategy clusterStrategy = ClusterStrategy.Proxy,
            TimeSpan? clusterIdleTimout = null,
            string user = null)
        {
            Queue = queue;
            ConfirmMessage = confirmMessage;
            ConfirmMessageTimeout = confirmMessageTimeout;
            ClusterStrategy = clusterStrategy;

            if (clusterIdleTimout != null)
            {
                ClusterIdleTimout = clusterIdleTimout.Value;
            }

            User = user;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Queue))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Queue) });
            }

            if (ClusterIdleTimout < TimeSpan.FromSeconds(1))
            {
                throw new ClientException(ErrorCode.ParameterLessThan, new object[] { nameof(ClusterIdleTimout), "one second" });
            }
        }
    }
}
