using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Clear queue from messages.
    /// </summary>
    public class ClearQueue : AbstractRequest<Confirmation>, IReplicable
    {
        /// <summary>
        /// Queue name.
        /// </summary>
        [DataMember(Name = "N")]
        public string Name { get; set; }

        /// <summary>
        /// Creates new ClearQueue command.
        /// </summary>
        public ClearQueue()
        {
        }

        /// <summary>
        /// Creates new ClearQueue command with params.
        /// </summary>
        /// <param name="name"></param>
        public ClearQueue(string name)
        {
            Name = name;
        }

        /// <inheritdoc/>
        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Name))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Name) });
            }
        }
    }
}
