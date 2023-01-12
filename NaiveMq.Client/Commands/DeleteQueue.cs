using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Delete queue.
    /// </summary>
    public class DeleteQueue : AbstractDeleteRequest<Confirmation>, IReplicable
    {
        /// <summary>
        /// Name.
        /// </summary>
        [DataMember(Name = "N")]
        public string Name { get; set; }

        /// <summary>
        /// Creates new DeleteQueue command.
        /// </summary>
        public DeleteQueue()
        {
        }

        /// <summary>
        /// Creates new DeleteQueue command with params.
        /// </summary>
        /// <param name="name"></param>
        public DeleteQueue(string name)
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
