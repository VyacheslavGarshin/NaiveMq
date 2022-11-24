namespace NaiveMq.Client.Commands
{
    public class Dequeue : AbstractRequest<DequeueResponse>
    {
        public string Queue { get; set; }
    }
}
