using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class LoginHandler : AbstractHandler<Login, Confirmation>
    {
        public override Task<Confirmation> ExecuteAsync(ClientContext context, Login command)
        {
            if (context.User == null)
            {
                if (context.Storage.Users.TryGetValue(command.Username, out var user)
                    && user.Entity.PasswordHash == command.Password.ComputeHash())
                {
                    context.User = user;
                }
                else
                {
                    throw new ServerException(ErrorCode.UserOrPasswordNotCorrect);
                }
            }
            else
            {
                throw new ServerException(ErrorCode.AlreadyLoggedIn);
            }

            return Task.FromResult(Confirmation.Ok(command));
        }
    }
}
