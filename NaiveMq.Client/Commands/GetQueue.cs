namespace NaiveMq.Client.Commands
{
    public class GetQueue : AbstractGetRequest<GetQueueResponse>
    {
        public string Name { get; set; }
    }
}
