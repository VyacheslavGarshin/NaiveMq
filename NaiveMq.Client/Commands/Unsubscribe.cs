using System.Runtime.Serialization;
using NaiveMq.Client.AbstractCommands;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Unsubscribe from the queue.
    /// </summary>
    public class Unsubscribe : AbstractRequest<Confirmation>
    {
        /// <summary>
        /// Queue name.
        /// </summary>
        [DataMember(Name = "Q")]
        public string Queue { get; set; }

        /// <summary>
        /// Creates new Unsubscribe command.
        /// </summary>
        public Unsubscribe()
        {
        }

        /// <summary>
        /// Creates new Unsubscribe command with params.
        /// </summary>
        /// <param name="queue"></param>
        public Unsubscribe(string queue)
        {
            Queue = queue;
        }

        /// <inheritdoc/>
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
