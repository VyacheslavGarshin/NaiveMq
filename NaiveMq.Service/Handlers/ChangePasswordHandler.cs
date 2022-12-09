using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class ChangePasswordHandler : IHandler<ChangePassword, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, ChangePassword command)
        {
            context.CheckUser(context);

            string oldPasswordHash = null;

            try
            {
                oldPasswordHash = context.User.PasswordHash;

                if (context.User.PasswordHash != command.CurrentPassword.ComputeHash())
                {
                    throw new ServerException(ErrorCode.WrongPassword);
                }

                if (command.NewPassword == command.CurrentPassword)
                {
                    throw new ServerException(ErrorCode.NewPasswordCannotBeTheSame);
                }

                if (string.IsNullOrEmpty(command.NewPassword))
                {
                    throw new ServerException(ErrorCode.PasswordCannotBeEmpty);
                }

                context.User.PasswordHash = command.NewPassword.ComputeHash();

                await context.Storage.PersistentStorage.SaveUserAsync(context.User, context.CancellationToken);
            }
            catch
            {
                context.User.PasswordHash = oldPasswordHash;

                throw;
            }

            return Confirmation.Ok(command);
        }

        public void Dispose()
        {
        }
    }
}
