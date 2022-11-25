namespace NaiveMq.Client.Entities
{
    public class UserEntity
    {
        public string Username { get; set; }

        public bool IsAdministrator { get; set; }

        public string PasswordHash { get; set; }

        public string HashAlgorithm { get; set; }
    }
}
