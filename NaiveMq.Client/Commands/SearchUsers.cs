using System.Runtime.Serialization;
using NaiveMq.Client.AbstractCommands;

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
        /// Creates new SearchUsers command.
        /// </summary>
        public SearchUsers()
        {
        }

        /// <summary>
        /// Creates new SearchUsers command with params.
        /// </summary>
        /// <param name="username"></param>
        public SearchUsers(string username)
        {
            Username = username;
        }
    }
}
