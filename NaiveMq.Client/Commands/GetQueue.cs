namespace NaiveMq.Client.Commands
{
    public class GetQueue : AbstractRequest<GetQueueResponse>
    {
        public string Name { get; set; }
    }
}
