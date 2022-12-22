using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Enums;
using Newtonsoft.Json;

namespace NaiveMq.Service.Entities
{
    public class MessageEntity
    {
        public Guid Id { get; set; }

        public string Tag { get; set; }

        public int? ClientId { get; set; }

        public string Queue { get; set; }

        public bool Request { get; set; }

        public Persistence Persistent { get; set; }

        public string RoutingKey { get; set; }

        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public int DataLength { get; set; }

        [JsonIgnore]
        public bool Delivered { get; set; }

        public static MessageEntity FromCommand(Message command)
        {
            return new MessageEntity
            {
                Id = command.Id,
                Tag = command.Tag,
                Queue = command.Queue,
                Request = command.Request,
                Persistent = command.Persistent,
                RoutingKey = command.RoutingKey,
                Data = command.Data.ToArray(), // materialize data from buffer
                DataLength = command.Data.Length,
            };
        }

        public MessageEntity Copy()
        {
            return new MessageEntity
            {
                Id = Id,
                Tag = Tag,
                ClientId = ClientId,
                Queue = Queue,
                Request = Request,
                Persistent = Persistent,
                RoutingKey = RoutingKey,
                Data = Data,
                DataLength = Data.Length,
            };
        }

        public Message ToCommand()
        {
            return new Message
            {
                Tag = Tag,
                Queue = Queue,
                Request = Request,
                Persistent = Persistent,
                RoutingKey = RoutingKey,
                Data = Data
            };
        }
    }
}
