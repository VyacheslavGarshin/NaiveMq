namespace NaiveMq.Client.Commands
{
    public class SearchUsers : AbstractRequest<SearchUsersResponse>
    {
        public string Username { get; set; }
    }
}
