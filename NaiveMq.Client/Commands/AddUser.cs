using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class AddUser : AbstractRequest<Confirmation>, IReplicable
    {
        [DataMember(Name = "U")]
        public string Username { get; set; }

        [DataMember(Name = "P")]
        public string Password { get; set; }

        [DataMember(Name = "A")]
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
