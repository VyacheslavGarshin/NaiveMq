using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Update user.
    /// </summary>
    public class UpdateUser : AbstractRequest<Confirmation>, IReplicable
    {
        /// <summary>
        /// Username to update.
        /// </summary>
        [DataMember(Name = "U")]
        public string Username { get; set; }

        /// <summary>
        /// Mark as administrator.
        /// </summary>
        [DataMember(Name = "A")]
        public bool Administrator { get; set; }

        /// <summary>
        /// Update password if not empty.
        /// </summary>
        [DataMember(Name = "P")]
        public string Password { get; set; }

        /// <summary>
        /// Creates new UpdateUser command.
        /// </summary>
        public UpdateUser()
        {
        }

        /// <summary>
        /// Creates new UpdateUser command with params.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="administrator"></param>
        /// <param name="password"></param>
        public UpdateUser(string username, bool administrator, string password)
        {
            Username = username;
            Administrator = administrator;
            Password = password;
        }

        /// <inheritdoc/>
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
