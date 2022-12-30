using NaiveMq.Client.Commands;
using NaiveMq.Client.Enums;
using System.Runtime.Serialization;

namespace NaiveMq.Service.Entities
{
    [DataContract]
    public class QueueEntity
    {
        [DataMember]
        public string User { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public bool Durable { get; set; }

        [DataMember]
        public bool Exchange { get; set; }

        [DataMember]
        public long? LengthLimit { get; set; }

        [DataMember]
        public long? VolumeLimit { get; set; }

        [DataMember]
        public LimitStrategy LimitStrategy { get; set; }

        public static QueueEntity FromCommand(AddQueue command)
        {
            return new QueueEntity
            {
                Name = command.Name,
                Durable = command.Durable,
                Exchange = command.Exchange,
                LengthLimit = command.LengthLimit,
                VolumeLimit = command.VolumeLimit,
                LimitStrategy = command.LimitStrategy,
            };
        }
    }
}
