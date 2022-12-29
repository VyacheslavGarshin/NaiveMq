using NaiveMq.Client.Commands;
using NaiveMq.Client.Enums;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace NaiveMq.Service.Entities
{
    public class MessageEntity
    {
        public Guid Id { get; set; }

        public string Tag { get; set; }

        public bool Request { get; set; }

        public Persistence Persistent { get; set; }

        public string RoutingKey { get; set; }

        [JsonIgnore]
        [IgnoreDataMember]
        public ReadOnlyMemory<byte> Data { get; set; }

        public int DataLength { get; set; }

        [JsonIgnore]
        [IgnoreDataMember]
        public int? ClientId { get; set; }

        [JsonIgnore]
        [IgnoreDataMember]
        public bool Delivered { get; set; }

        public static MessageEntity FromCommand(Message command, int clientId)
        {
            return new MessageEntity
            {
                Id = command.Id,
                Tag = command.Tag,
                Request = command.Request,
                Persistent = command.Persistent,
                RoutingKey = command.RoutingKey,
                Data = command.Data.ToArray(), // materialize data from buffer
                DataLength = command.Data.Length,
                ClientId = clientId,
            };
        }

        public MessageEntity Copy()
        {
            return new MessageEntity
            {
                Id = Id,
                Tag = Tag,
                Request = Request,
                Persistent = Persistent,
                RoutingKey = RoutingKey,
                Data = Data,
                DataLength = Data.Length,
                ClientId = ClientId,
            };
        }

        public Message ToCommand(string queue)
        {
            return new Message
            {
                Tag = Tag,
                Queue = queue,
                Request = Request,
                Persistent = Persistent,
                RoutingKey = RoutingKey,
                Data = Data
            };
        }
    }
}
