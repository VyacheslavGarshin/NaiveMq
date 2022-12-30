using NaiveMq.Client.Enums;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class SearchQueues : AbstractSearchRequest<SearchQueuesResponse>
    {
        /// <summary>
        /// Search by user.
        /// </summary>
        /// <remarks>If user is an administrator.</remarks>
        [DataMember(Name = "U")]
        public string User { get; set; }

        [DataMember(Name = "N")]
        public string Name { get; set; }

        [DataMember(Name = "St")]
        public QueueStatus? Status { get; set; }

        public SearchQueues()
        {
        }

        public SearchQueues(string user = null, string name = null, QueueStatus? status = null)
        {
            User = user;
            Name = name;
            Status = status;
        }
    }
}
