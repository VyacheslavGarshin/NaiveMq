using System.Runtime.Serialization;
using NaiveMq.Client.AbstractCommands;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Login.
    /// </summary>
    public class Login : AbstractRequest<Confirmation>
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
        /// Creates new Login command.
        /// </summary>
        public Login()
        {
        }

        /// <summary>
        /// Creates new Login command with params.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public Login(string username, string password)
        {
            Username = username;
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

            if (string.IsNullOrEmpty(Password))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Password) });
            }
        }
    }
}
