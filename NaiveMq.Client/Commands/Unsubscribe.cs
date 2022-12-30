using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class Unsubscribe : AbstractRequest<Confirmation>
    {
        [DataMember(Name = "Q")]
        public string Queue { get; set; }

        public Unsubscribe()
        {
        }

        public Unsubscribe(string queue)
        {
            Queue = queue;
        }

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
