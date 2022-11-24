namespace NaiveMq.Client.Commands
{
    public class Enqueue : AbstractRequest<Confirmation>
    {
        public string Queue { get; set; }

        public string Text { get; set; }
    }
}
