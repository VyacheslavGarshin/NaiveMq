using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class ChangePassword : AbstractRequest<Confirmation>, IReplicable
    {
        [DataMember(Name = "CP")]
        public string CurrentPassword { get; set; }

        [DataMember(Name = "NP")]
        public string NewPassword { get; set; }

        public ChangePassword()
        {
        }

        public ChangePassword(string currentPassword, string newPassword)
        {
            CurrentPassword = currentPassword;
            NewPassword = newPassword;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(CurrentPassword))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(CurrentPassword) });
            }

            if (string.IsNullOrEmpty(NewPassword))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(NewPassword) });
            }
        }
    }
}
