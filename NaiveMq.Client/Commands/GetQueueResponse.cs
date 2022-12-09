using NaiveMq.Client.Dto;

namespace NaiveMq.Client.Commands
{
    public class GetQueueResponse : AbstractResponse<GetQueueResponse>
    {
        public QueueDto Queue { get; set; }
    }
}
