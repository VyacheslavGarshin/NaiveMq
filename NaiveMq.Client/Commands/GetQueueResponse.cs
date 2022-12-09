using NaiveMq.Client.Dto;

namespace NaiveMq.Client.Commands
{
    public class GetQueueResponse : AbstractResponse<GetQueueResponse>
    {
        public Queue Queue { get; set; }
    }
}
