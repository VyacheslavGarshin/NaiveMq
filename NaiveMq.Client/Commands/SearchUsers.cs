using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Search users.
    /// </summary>
    public class SearchUsers : AbstractSearchRequest<SearchUsersResponse>
    {
        /// <summary>
        /// Username.
        /// </summary>
        [DataMember(Name = "U")]
        public string Username { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public SearchUsers()
        {
        }

        /// <summary>
        /// Constructor with params.
        /// </summary>
        /// <param name="username"></param>
        public SearchUsers(string username)
        {
            Username = username;
        }
    }
}
