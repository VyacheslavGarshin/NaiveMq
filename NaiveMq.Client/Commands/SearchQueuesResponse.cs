using NaiveMq.Client.Dto;
using System.Collections.Generic;

namespace NaiveMq.Client.Commands
{
    public class SearchQueuesResponse : AbstractResponse<SearchQueuesResponse>
    {
        public List<Queue> Queues { get; set; }
    }
}
