using NaiveMq.Client.Dto;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Information about cluster activity for the queue.
    /// </summary>
    public class ClusterQueueHint : AbstractRequest<Confirmation>
    {
        /// <summary>
        /// Queue name.
        /// </summary>
        [DataMember(Name = "N")]
        public string Name { get; set; }

        /// <summary>
        /// Hints.
        /// </summary>
        [DataMember(Name = "H")]
        public List<QueueHint> Hints { get; set; }

        /// <summary>
        /// Creates new ClusterQueueHint command.
        /// </summary>
        public ClusterQueueHint()
        {
        }

        /// <summary>
        /// Creates new ClusterQueueHint command params.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="hints"></param>
        public ClusterQueueHint(string name, List<QueueHint> hints)
        {
            Name = name;
            Hints = hints;
        }

        /// <inheritdoc/>
        public override void Validate()
        {
            base.Validate();

            if (Hints?.Count < 1)
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Hints) });
            }
        }
    }
}
