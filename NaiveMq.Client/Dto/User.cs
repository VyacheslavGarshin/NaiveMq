using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    [DataContract]
    public class User
    {
        [DataMember(Name = "U")]
        public string Username { get; set; }

        [DataMember(Name = "A")]
        public bool Administrator { get; set; }
    }
}
