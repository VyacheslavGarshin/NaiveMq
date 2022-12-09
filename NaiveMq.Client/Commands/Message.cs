using NaiveMq.Client.Enums;
using Newtonsoft.Json;

namespace NaiveMq.Client.Commands
{
    public class Message : AbstractRequest<Confirmation>, IDataCommand
    {
        public string Queue { get; set; }

        public bool Request { get; set; }

        public Persistent Persistent { get; set; }

        public string RoutingKey { get; set; }

        [JsonIgnore]
        public byte[] Data { get; set; }

        public override void Validate()
        {
            base.Validate();

            if (Request && Persistent != Persistent.No)
            {
                throw new ClientException(ErrorCode.PersistentRequest);
            }
        }
    }
}
