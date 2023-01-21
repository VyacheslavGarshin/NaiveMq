using System.Runtime.Serialization;
using NaiveMq.Client.AbstractCommands;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Delete binding.
    /// </summary>
    public class DeleteBinding : AbstractDeleteRequest<Confirmation>, IReplicable
    {
        /// <summary>
        /// Exchange queue.
        /// </summary>
        [DataMember(Name = "E")]
        public string Exchange { get; set; }

        /// <summary>
        /// Bound queue.
        /// </summary>
        [DataMember(Name = "Q")]
        public string Queue { get; set; }

        /// <summary>
        /// Creates new DeleteBinding command.
        /// </summary>
        public DeleteBinding()
        {
        }

        /// <summary>
        /// Creates new DeleteBinding command with params.
        /// </summary>
        /// <param name="exchange"></param>
        /// <param name="queue"></param>
        /// <param name="try"></param>
        public DeleteBinding(string exchange, string queue, bool @try = false) : base(@try)
        {
            Exchange = exchange;
            Queue = queue;
        }

        /// <inheritdoc/>
        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Exchange))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Exchange) });
            }

            if (string.IsNullOrEmpty(Queue))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Queue) });
            }
        }
    }
}
