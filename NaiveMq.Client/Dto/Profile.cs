using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    /// <summary>
    /// User profile.
    /// </summary>
    [DataContract]
    public class Profile
    {
        /// <summary>
        /// User name.
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
