namespace NaiveMq.Client.Commands
{
    public class AddUser : AbstractRequest<Confirmation>
    {
        public string Username { get; set; }

        public bool IsAdministrator { get; set; }

        public string PasswordHash { get; set; }

        public string HashAlgorithm { get; set; }
    }
}
