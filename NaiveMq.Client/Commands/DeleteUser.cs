using System.Runtime.Serialization;
using NaiveMq.Client.AbstractCommands;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Delete user.
    /// </summary>
    public class DeleteUser : AbstractDeleteRequest<Confirmation>, IReplicable
    {
        /// <summary>
        /// Username.
        /// </summary>
        [DataMember(Name = "U")]
        public string Username { get; set; }

        /// <summary>
        /// Creates new DeleteUser command.
        /// </summary>
        public DeleteUser()
        {
        }

        /// <summary>
        /// Creates new DeleteUser command with params.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="try"></param>
        public DeleteUser(string username, bool @try = false) : base(@try)
        {
            Username = username;
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
