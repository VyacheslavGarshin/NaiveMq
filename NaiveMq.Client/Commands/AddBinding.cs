using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class AddBinding : AbstractRequest<Confirmation>, IReplicable
    {
        [DataMember(Name = "E")]
        public string Exchange { get; set; }

        [DataMember(Name = "Q")]
        public string Queue { get; set; }

        [DataMember(Name = "D")]
        public bool Durable { get; set; }

        /// <summary>
        /// Reqex pattern.
        /// </summary>
        /// <remarks>If null or empty then any message will go to the queue.</remarks>
        public string Pattern { get; set; }

        public AddBinding()
        {
        }

        public AddBinding(string exchange, string queue, bool durable = false, string pattern = null)
        {
            Exchange = exchange;
            Queue = queue;
            Durable = durable;
            Pattern = pattern;
        }

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
