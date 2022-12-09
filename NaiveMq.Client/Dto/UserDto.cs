namespace NaiveMq.Client.Dto
{
    public class UserDto
    {
        public string Username { get; set; }

        public bool Administrator { get; set; }

        public string PasswordHash { get; set; }
    }
}
