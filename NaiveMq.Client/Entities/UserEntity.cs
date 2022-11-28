namespace NaiveMq.Client.Entities
{
    public class UserEntity
    {
        public string Username { get; set; }

        public bool Administrator { get; set; }

        public string PasswordHash { get; set; }
    }
}
