using NaiveMq.Client.Enums;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Search queues.
    /// </summary>
    public class SearchQueues : AbstractSearchRequest<SearchQueuesResponse>
    {
        /// <summary>
        /// User username.
        /// </summary>
        /// <remarks>If user is an administrator.</remarks>
        [DataMember(Name = "U")]
        public string User { get; set; }

        /// <summary>
        /// Queue name.
        /// </summary>
        [DataMember(Name = "N")]
        public string Name { get; set; }

        /// <summary>
        /// Status.
        /// </summary>
        [DataMember(Name = "St")]
        public QueueStatus? Status { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public SearchQueues()
        {
        }

        /// <summary>
        /// Constructor with params.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="name"></param>
        /// <param name="status"></param>
        public SearchQueues(string user = null, string name = null, QueueStatus? status = null)
        {
            User = user;
            Name = name;
            Status = status;
        }
    }
}
