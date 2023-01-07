using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    /// <summary>
    /// Binding.
    /// </summary>
    [DataContract]
    public class Binding
    {
        /// <summary>
        /// Exchange queue.
        /// </summary>
        [DataMember(Name = "E")]
        public string Exchange { get; set; }

        /// <summary>
        /// Bound queue.
        /// </summary>
        [DataMember(Name = "Q")]
        public string Queue { get; set; }

        /// <summary>
        /// Save.
        /// </summary>
        [DataMember(Name = "D")]
        public bool Durable { get; set; }

        /// <summary>
        /// RegEx pattern.
        /// </summary>
        [DataMember(Name = "P")]
        public string Pattern { get; set; }
    }
}
