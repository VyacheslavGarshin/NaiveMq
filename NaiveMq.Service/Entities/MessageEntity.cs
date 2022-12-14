using NaiveMq.Client.Enums;
using Newtonsoft.Json;

namespace NaiveMq.Service.Entities
{
    public class MessageEntity
    {
        public Guid Id { get; set; }

        public int? ClientId { get; set; }

        public string Queue { get; set; }

        public bool Request { get; set; }

        public Persistence Persistent { get; set; }

        public string RoutingKey { get; set; }

        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public int DataLength { get; set; }
    }
}
