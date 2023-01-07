using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    /// <summary>
    /// Server.
    /// </summary>
    [DataContract]
    public class Server
    {
        /// <summary>
        /// Name.
        /// </summary>
        [DataMember(Name = "N")]
        public string Name { get; set; }

        /// <summary>
        /// Cluster key.
        /// </summary>
        [DataMember(Name = "CK")]
        public string ClusterKey { get; set; }
    }
}
