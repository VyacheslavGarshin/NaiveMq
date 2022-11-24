namespace NaiveMq.Client.Commands
{
    public class Subscribe : AbstractRequest<Confirmation>
    {
        public string Queue { get; set; }
    }
}
