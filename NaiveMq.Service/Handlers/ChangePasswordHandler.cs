using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Client.Cogs;

namespace NaiveMq.Service.Handlers
{
    public class ChangePasswordHandler : AbstractHandler<ChangePassword, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, ChangePassword command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            var oldPasswordHash = context.User.Entity.PasswordHash;

            if (context.User.Entity.PasswordHash != command.CurrentPassword.ComputeHash())
            {
                throw new ServerException(ErrorCode.WrongPassword);
            }

            if (command.NewPassword == command.CurrentPassword)
            {
                throw new ServerException(ErrorCode.NewPasswordTheSame);
            }

            if (string.IsNullOrEmpty(command.NewPassword))
            {
                throw new ServerException(ErrorCode.PasswordEmpty);
            }

            context.User.Entity.PasswordHash = command.NewPassword.ComputeHash();

            try
            {
                await context.Storage.PersistentStorage.SaveUserAsync(context.User.Entity, cancellationToken);
            }
            catch
            {
                context.User.Entity.PasswordHash = oldPasswordHash;

                throw;
            }

            return Confirmation.Ok(command);
        }
    }
}
