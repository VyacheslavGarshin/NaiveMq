using NaiveMq.Client.Enums;
using Newtonsoft.Json;
using System;

namespace NaiveMq.Client.Dto
{
    public class Message
    {
        public Guid Id { get; set; }

        public int? ClientId { get; set; }

        public string Queue { get; set; }

        public bool Request { get; set; }

        public Persistence Persistent { get; set; }

        public string RoutingKey { get; set; }

        [JsonIgnore]
        public byte[] Data { get; set; }

        public int DataLength { get; set; }
    }
}
