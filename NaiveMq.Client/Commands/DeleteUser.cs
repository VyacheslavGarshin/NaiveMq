namespace NaiveMq.Client.Commands
{
    public class DeleteUser : AbstractRequest<Confirmation>
    {
        public string Username { get; set; }
    }
}
