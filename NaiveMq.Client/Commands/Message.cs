using Newtonsoft.Json;

namespace NaiveMq.Client.Commands
{
    public class Message : AbstractRequest<Confirmation>, IDataCommand
    {
        public string Queue { get; set; }

        public bool Request { get; set; }

        public bool Durable { get; set; }

        public string BindingKey { get; set; }

        [JsonIgnore]
        public byte[] Data { get; set; }
    }
}
