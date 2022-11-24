using NaiveMq.Client.Entities;

namespace NaiveMq.Client.Commands
{
    public class GetQueueResponse : AbstractResponse<GetQueueResponse>
    {
        public QueueEntity Queue { get; set; }
    }
}
