using NaiveMq.Client.Entities;
using System.Collections.Generic;

namespace NaiveMq.Client.Commands
{
    public class SearchQueuesResponse : AbstractResponse<SearchQueuesResponse>
    {
        public List<QueueEntity> Queues { get; set; }
    }
}
