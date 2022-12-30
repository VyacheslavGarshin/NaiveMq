using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class GetQueue : AbstractGetRequest<GetQueueResponse>
    {
        [DataMember(Name = "N")]
        public string Name { get; set; }

        public GetQueue()
        {
        }

        public GetQueue(string name)
        {
            Name = name;
        }

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
