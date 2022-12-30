using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class SearchUsers : AbstractSearchRequest<SearchUsersResponse>
    {
        [DataMember(Name = "U")]
        public string Username { get; set; }

        public SearchUsers()
        {
        }

        public SearchUsers(string username)
        {
            Username = username;
        }
    }
}
