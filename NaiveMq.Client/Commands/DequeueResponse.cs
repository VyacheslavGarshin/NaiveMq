using NaiveMq.Client.Entities;

namespace NaiveMq.Client.Commands
{
    public class DequeueResponse : AbstractResponse<DequeueResponse>
    {
        public MessageEntity Message { get; set; }
    }
}
