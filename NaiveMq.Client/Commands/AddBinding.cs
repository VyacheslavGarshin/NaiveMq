namespace NaiveMq.Client.Commands
{
    public class AddBinding : AbstractRequest<Confirmation>
    {
        public string Exchange { get; set; }

        public string Queue { get; set; }

        public bool Durable { get; set; }

        public string Regex { get; set; }
    }
}
