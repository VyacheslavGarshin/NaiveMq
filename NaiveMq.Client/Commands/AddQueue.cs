namespace NaiveMq.Client.Commands
{
    public class AddQueue : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }

        public bool Durable { get; set; }
    }
}
