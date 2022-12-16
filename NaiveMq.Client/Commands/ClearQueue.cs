namespace NaiveMq.Client.Commands
{
    public class ClearQueue : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }
    }
}
