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
        /// Constructor.
        /// </summary>
        public GetQueue()
        {
        }

        /// <summary>
        /// Constructor with params.
        /// </summary>
        /// <param name="name"></param>
        public GetQueue(string name)
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
