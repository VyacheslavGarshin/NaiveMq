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
        /// Constructor.
        /// </summary>
        public GetUser()
        {
        }

        /// <summary>
        /// Constructor with params.
        /// </summary>
        /// <param name="username"></param>
        public GetUser(string username)
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
