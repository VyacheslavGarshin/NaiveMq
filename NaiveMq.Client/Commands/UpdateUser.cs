using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class UpdateUser : AbstractRequest<Confirmation>, IReplicable
    {
        [DataMember(Name = "U")]
        public string Username { get; set; }

        [DataMember(Name = "A")]
        public bool Administrator { get; set; }

        /// <summary>
        /// Update password if not empty.
        /// </summary>
        [DataMember(Name = "P")]
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
