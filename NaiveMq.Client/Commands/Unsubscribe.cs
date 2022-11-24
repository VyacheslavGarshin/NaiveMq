namespace NaiveMq.Client.Commands
{
    public class Unsubscribe : AbstractRequest<Confirmation>
    {
        public string Queue { get; set; }
    }
}
