using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    public class Server
    {
        [DataMember(Name = "N")]
        public string Name { get; set; }

        [DataMember(Name = "CK")]
        public string ClusterKey { get; set; }
    }
}
