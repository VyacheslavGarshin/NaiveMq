using System.Runtime.Serialization;
using NaiveMq.Client.AbstractCommands;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Add new user.
    /// </summary>
    public class AddUser : AbstractAddRequest<Confirmation>, IReplicable
    {
        /// <summary>
        /// Username.
        /// </summary>
        [DataMember(Name = "U")]
        public string Username { get; set; }

        /// <summary>
        /// Password.
        /// </summary>
        [DataMember(Name = "P")]
        public string Password { get; set; }

        /// <summary>
        /// Mark user as administrator.
        /// </summary>
        [DataMember(Name = "A")]
        public bool Administrator { get; set; }

        /// <summary>
        /// Creates new AddUser command.
        /// </summary>
        public AddUser()
        {
        }

        /// <summary>
        /// Creates new AddUser command with params.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="administrator"></param>
        /// <param name="try"></param>
        public AddUser(string username, string password, bool administrator = false, bool @try = false) : base(@try)
        {
            Username = username;
            Password = password;
            Administrator = administrator;
        }

        /// <inheritdoc/>
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
