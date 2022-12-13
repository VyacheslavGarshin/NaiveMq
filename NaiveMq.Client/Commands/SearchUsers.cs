namespace NaiveMq.Client.Commands
{
    public class SearchUsers : AbstractSearchRequest<SearchUsersResponse>
    {
        public string Username { get; set; }
    }
}
