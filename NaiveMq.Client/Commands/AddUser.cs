namespace NaiveMq.Client.Commands
{
    public class AddUser : AbstractRequest<Confirmation>, IReplicable
    {
        public string Username { get; set; }

        public string Password { get; set; }

        public bool Administrator { get; set; }

        public AddUser()
        {
        }

        public AddUser(string username, string password, bool administrator = false)
        {
            Username = username;
            Password = password;
            Administrator = administrator;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Username))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Username) });
            }

            if (string.IsNullOrEmpty(Password))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Password) });
            }
        }
    }
}
