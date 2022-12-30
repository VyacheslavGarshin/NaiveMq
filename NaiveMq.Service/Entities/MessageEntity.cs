using NaiveMq.Client.Commands;
using NaiveMq.Client.Enums;
using System.Runtime.Serialization;

namespace NaiveMq.Service.Entities
{
    [DataContract]
    public class MessageEntity
    {
        [DataMember(Name = "I")]
        public Guid Id { get; set; }

        [DataMember(Name = "T")]
        public string Tag { get; set; }

        [DataMember(Name = "R")]
        public bool Request { get; set; }

        [DataMember(Name = "P")]
        public Persistence Persistent { get; set; }

        [DataMember(Name = "RK")]
        public string RoutingKey { get; set; }

        [IgnoreDataMember]
        public ReadOnlyMemory<byte> Data { get; set; }

        [DataMember(Name = "DL")]
        public int DataLength { get; set; }

        [IgnoreDataMember]
        public int? ClientId { get; set; }

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
