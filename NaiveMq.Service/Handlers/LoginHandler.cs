using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Client.Cogs;

namespace NaiveMq.Service.Handlers
{
    public class LoginHandler : AbstractHandler<Login, Confirmation>
    {
        public override Task<Confirmation> ExecuteAsync(ClientContext context, Login command, CancellationToken cancellationToken)
        {
            if (context.User != null)
            {
                throw new ServerException(ErrorCode.AlreadyLoggedIn);
            }

            if (!(context.Storage.Users.TryGetValue(command.Username, out var user)
                    && user.Entity.PasswordHash == command.Password.ComputeHash()))
            {
                throw new ServerException(ErrorCode.UserOrPasswordNotCorrect);
            }

            context.User = user;

            return Task.FromResult(Confirmation.Ok(command));
        }
    }
}
