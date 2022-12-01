namespace NaiveMq.Client.Commands
{
    public class Confirmation : AbstractResponse<Confirmation>
    {
        public string Text { get; set; }
    }
}
