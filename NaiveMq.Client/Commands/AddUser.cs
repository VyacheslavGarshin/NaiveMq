namespace NaiveMq.Client.Commands
{
    public class AddUser : AbstractRequest<Confirmation>
    {
        public string Username { get; set; }

        public bool Administrator { get; set; }

        public string Password { get; set; }
    }
}
