using NaiveMq.Client.Enums;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    /// <summary>
    /// Queue.
    /// </summary>
    [DataContract]
    public class Queue
    {
        /// <summary>
        /// User.
        /// </summary>
        [DataMember(Name = "U")]
        public string User { get; set; }

        /// <summary>
        /// Name.
        /// </summary>
        [DataMember(Name = "N")]
        public string Name { get; set; }

        /// <summary>
        /// Is saved.
        /// </summary>
        [DataMember(Name = "D")]
        public bool Durable { get; set; }

        /// <summary>
        /// Is exchange.
        /// </summary>
        [DataMember(Name = "E")]
        public bool Exchange { get; set; }

        /// <summary>
        /// Length limit.
        /// </summary>
        [DataMember(Name = "LL")]
        public long? LengthLimit { get; set; }

        /// <summary>
        /// Volume limit.
        /// </summary>
        [DataMember(Name = "VL")]
        public long? VolumeLimit { get; set; }

        /// <summary>
        /// Limit strategy.
        /// </summary>
        [DataMember(Name = "LS")]
        public LimitStrategy LimitStrategy { get; set; }

        /// <summary>
        /// Status.
        /// </summary>
        [DataMember(Name = "S")]
        public QueueStatus Status { get; set; }
    }
}
