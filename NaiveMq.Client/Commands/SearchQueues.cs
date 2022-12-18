namespace NaiveMq.Client.Commands
{
    public class SearchQueues : AbstractSearchRequest<SearchQueuesResponse>
    {
        /// <summary>
        /// Search by user.
        /// </summary>
        /// <remarks>If user is an administrator.</remarks>
        public string User { get; set; }

        public string Name { get; set; }

        public SearchQueues()
        {
        }

        public SearchQueues(string user, string name)
        {
            User = user;
            Name = name;
        }
    }
}
