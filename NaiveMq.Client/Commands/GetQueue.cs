using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Get queue.
    /// </summary>
    public class GetQueue : AbstractGetRequest<GetQueueResponse>
    {
        /// <summary>
        /// Name.
        /// </summary>
        [DataMember(Name = "N")]
        public string Name { get; set; }

        /// <summary>
        /// Creates new GetQueue command.
        /// </summary>
        public GetQueue()
        {
        }

        /// <summary>
        /// Creates new GetQueue command with params.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="try"></param>
        public GetQueue(string name, bool @try = false) : base(@try) 
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
