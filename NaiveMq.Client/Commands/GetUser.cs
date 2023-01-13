using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Get user.
    /// </summary>
    public class GetUser : AbstractGetRequest<GetUserResponse>
    {
        /// <summary>
        /// Username.
        /// </summary>
        [DataMember(Name = "U")]
        public string Username { get; set; }

        /// <summary>
        /// Creates new GetUser command.
        /// </summary>
        public GetUser()
        {
        }

        /// <summary>
        /// Creates new GetUser command with params.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="try"></param>
        public GetUser(string username, bool @try = false) : base(@try)
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
