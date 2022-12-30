using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    public class Binding
    {
        [DataMember(Name = "E")]
        public string Exchange { get; set; }

        [DataMember(Name = "Q")]
        public string Queue { get; set; }

        [DataMember(Name = "D")]
        public bool Durable { get; set; }

        [DataMember(Name = "P")]
        public string Pattern { get; set; }
    }
}
