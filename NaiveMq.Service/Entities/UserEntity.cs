using NaiveMq.Client.Common;
using NaiveMq.Client.Commands;
using System.Runtime.Serialization;

namespace NaiveMq.Service.Entities
{
    [DataContract]
    public class UserEntity
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public bool Administrator { get; set; }

        [DataMember]
        public string PasswordHash { get; set; }

        public static UserEntity FromCommand(AddUser command)
        {
            return new UserEntity
            {
                Username = command.Username,
                Administrator = command.Administrator,
                PasswordHash = command.Password.ComputeHash(),
            };
        }
    }
}
