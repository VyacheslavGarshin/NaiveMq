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
    }
}
