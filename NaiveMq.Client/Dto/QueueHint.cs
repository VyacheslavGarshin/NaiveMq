using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    /// <summary>
    /// Queue hint.
    /// </summary>
    [DataContract]
    public class QueueHint
    {
        /// <summary>
        /// Host.
        /// </summary>
        [DataMember(Name = "H")]
        public string Host { get; set; }

        /// <summary>
        /// Queue length on the host.
        /// </summary>
        [DataMember(Name = "L")]
        public long Length { get; set; }

        /// <summary>
        /// Number of subscriptions on the host.
        /// </summary>
        [DataMember(Name = "S")]
        public long Subscriptions { get; set; }
    }
}
