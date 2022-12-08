using Newtonsoft.Json;
using System;

namespace NaiveMq.Client.Entities
{
    public class MessageEntity
    {
        public Guid Id { get; set; }

        public int ClientId { get; set; }

        public string Queue { get; set; }

        public bool Request { get; set; }

        public bool Persistent { get; set; }

        public string RoutingKey { get; set; }

        [JsonIgnore]
        public byte[] Data
        {
            get { return _data; }
            set
            {
                _data = value;
                DataLength = _data?.Length;
            }
        }

        public int? DataLength { get; set; }

        private byte[] _data;
    }
}
