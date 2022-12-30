using NaiveMq.Client.Enums;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    public class Queue
    {
        [DataMember(Name = "U")]
        public string User { get; set; }

        [DataMember(Name = "N")]
        public string Name { get; set; }

        [DataMember(Name = "D")]
        public bool Durable { get; set; }

        [DataMember(Name = "E")]
        public bool Exchange { get; set; }

        [DataMember(Name = "LL")]
        public long? LengthLimit { get; set; }

        [DataMember(Name = "VL")]
        public long? VolumeLimit { get; set; }

        [DataMember(Name = "LS")]
        public LimitStrategy LimitStrategy { get; set; }

        [DataMember(Name = "S")]
        public QueueStatus Status { get; set; }
    }
}
