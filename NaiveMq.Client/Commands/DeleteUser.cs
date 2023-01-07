using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Delete user.
    /// </summary>
    public class DeleteUser : AbstractRequest<Confirmation>, IReplicable
    {
        /// <summary>
        /// Username.
        /// </summary>
        [DataMember(Name = "U")]
        public string Username { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public DeleteUser()
        {
        }

        /// <summary>
        /// Constructor with params.
        /// </summary>
        /// <param name="username"></param>
        public DeleteUser(string username)
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
