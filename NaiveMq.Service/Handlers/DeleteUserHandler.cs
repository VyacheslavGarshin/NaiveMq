using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Service.Enums;

namespace NaiveMq.Service.Handlers
{
    public class DeleteUserHandler : AbstractHandler<DeleteUser, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, DeleteUser command, CancellationToken cancellationToken)
        {
            context.CheckAdmin();

            UserCog user = null;

            try
            {
                if (string.Equals(context.User.Entity.Username, command.Username, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new ServerException(ErrorCode.UserDeleteSelf);
                }

                if (!context.Storage.Users.TryRemove(command.Username, out user))
                {
                    throw new ServerException(ErrorCode.UserNotFound, new object[] { command.Username });
                }

                if (context.Mode == ClientContextMode.Client)
                {
                    await context.Storage.PersistentStorage.DeleteUserAsync(command.Username, cancellationToken);
                }

                user.Dispose();
            }
            catch
            {
                context.Storage.Users.TryAdd(command.Username, user);
                throw;
            }

            return Confirmation.Ok(command);
        }
    }
}
