using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class LoginHandler : IHandler<Login, Confirmation>
    {
        public Task<Confirmation> ExecuteAsync(HandlerContext context, Login command)
        {           
            if (context.Storage.Users.TryGetValue(command.Username, out var userEntity)
                && userEntity.PasswordHash == command.Password.ComputeHash())
            {
                context.User = userEntity;
            }
            else
            {
                throw new ServerException(ErrorCode.UserOrPasswordNotCorrect, ErrorCode.UserOrPasswordNotCorrect.GetDescription());
            }

            return Task.FromResult((Confirmation)null);
        }

        public void Dispose()
        {
        }
    }
}
