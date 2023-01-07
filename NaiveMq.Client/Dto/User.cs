using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    /// <summary>
    /// User.
    /// </summary>
    [DataContract]
    public class User
    {
        /// <summary>
        /// Username.
        /// </summary>
        [DataMember(Name = "U")]
        public string Username { get; set; }

        /// <summary>
        /// Is administrator.
        /// </summary>
        [DataMember(Name = "A")]
        public bool Administrator { get; set; }
    }
}
