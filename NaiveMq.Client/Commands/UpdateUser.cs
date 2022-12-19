namespace NaiveMq.Client.Commands
{
    public class UpdateUser : AbstractRequest<Confirmation>, IReplicable
    {
        public string Username { get; set; }

        public bool Administrator { get; set; }

        /// <summary>
        /// Update password if not empty.
        /// </summary>
        public string Password { get; set; }

        public UpdateUser()
        {
        }

        public UpdateUser(string username, bool administrator, string password)
        {
            Username = username;
            Administrator = administrator;
            Password = password;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Username))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Username) });
            }
        }
    }
}
