namespace NaiveMq.Client.Commands
{
    public class DeleteQueue : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }
    }
}
