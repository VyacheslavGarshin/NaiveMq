using NaiveMq.Client.Common;
using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Entities
{
    public class UserEntity
    {
        public string Username { get; set; }

        public bool Administrator { get; set; }

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
