namespace NaiveMq.Client.Commands
{
    public class Message : AbstractRequest<Confirmation>
    {
        public string Queue { get; set; }

        public bool Durable { get; set; }

        public string Text { get; set; }
    }
}
