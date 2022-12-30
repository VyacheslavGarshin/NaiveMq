using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    [DataContract]
    public class QueueHint
    {
        [DataMember(Name = "N")]
        public string Name { get; set; }

        [DataMember(Name = "H")]
        public string Host { get; set; }

        [DataMember(Name = "L")]
        public long Length { get; set; }

        [DataMember(Name = "S")]
        public long Subscriptions { get; set; }
    }
}
