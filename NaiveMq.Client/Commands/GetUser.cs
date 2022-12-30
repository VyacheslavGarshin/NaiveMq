using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class GetUser : AbstractGetRequest<GetUserResponse>
    {
        [DataMember(Name = "U")]
        public string Username { get; set; }

        public GetUser()
        {
        }

        public GetUser(string username)
        {
            Username = username;
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
