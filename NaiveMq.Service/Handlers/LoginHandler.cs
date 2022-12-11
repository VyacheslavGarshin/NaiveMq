using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class LoginHandler : IHandler<Login, Confirmation>
    {
        public Task<Confirmation> ExecuteAsync(ClientContext context, Login command)
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

            return Task.FromResult(Confirmation.Ok(command));
        }

        public void Dispose()
        {
        }
    }
}
