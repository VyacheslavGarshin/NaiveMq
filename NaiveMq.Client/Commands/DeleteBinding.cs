namespace NaiveMq.Client.Commands
{
    public class DeleteBinding : AbstractRequest<Confirmation>
    {
        public string Exchange { get; set; }

        public string Queue { get; set; }
    }
}
