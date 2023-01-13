using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Add binding between exchange and queue.
    /// </summary>
    public class AddBinding : AbstractAddRequest<Confirmation>, IReplicable
    {
        /// <summary>
        /// Exchange queue created with <see cref="AddQueue.Exchange"/> set to true.
        /// </summary>
        [DataMember(Name = "E")]
        public string Exchange { get; set; }

        /// <summary>
        /// Bind queue.
        /// </summary>
        [DataMember(Name = "Q")]
        public string Queue { get; set; }

        /// <summary>
        /// Save.
        /// </summary>
        [DataMember(Name = "D")]
        public bool Durable { get; set; }

        /// <summary>
        /// Reqex pattern.
        /// </summary>
        /// <remarks>If null or empty then any message will go to the queue.</remarks>
        [DataMember(Name = "P")]
        public string Pattern { get; set; }

        /// <summary>
        /// Creates new AddBinding command.
        /// </summary>
        public AddBinding()
        {
        }

        /// <summary>
        /// Creates new AddBinding command with params.
        /// </summary>
        /// <param name="exchange"></param>
        /// <param name="queue"></param>
        /// <param name="durable"></param>
        /// <param name="pattern"></param>
        /// <param name="try"></param>
        public AddBinding(string exchange, string queue, bool durable = false, string pattern = null, bool @try = false) : base(@try)
        {
            Exchange = exchange;
            Queue = queue;
            Durable = durable;
            Pattern = pattern;
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
