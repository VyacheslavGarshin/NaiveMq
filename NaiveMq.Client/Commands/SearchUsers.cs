namespace NaiveMq.Client.Commands
{
    public class SearchUsers : AbstractSearchRequest<SearchUsersResponse>
    {
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
