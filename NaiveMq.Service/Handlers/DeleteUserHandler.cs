using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Service.Enums;
using NaiveMq.Client.Enums;

namespace NaiveMq.Service.Handlers
{
    public class DeleteUserHandler : AbstractHandler<DeleteUser, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, DeleteUser command, CancellationToken cancellationToken)
        {
            context.CheckAdmin();

            if (string.Equals(context.User.Entity.Username, command.Username, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ServerException(ErrorCode.UserDeleteSelf);
            }

            if (context.Storage.Users.TryGetValue(command.Username, out var user))
            {
                user.SetStatus(UserStatus.Deleting);
            }
            else
            {
                throw new ServerException(ErrorCode.UserNotFound, new[] { command.Username });
            }

            if (context.Mode == ClientContextMode.Client)
            {
                await context.Storage.PersistentStorage.DeleteUserAsync(command.Username, cancellationToken);
            }

            user.Dispose();
            user.SetStatus(UserStatus.Deleted);
            context.Storage.Users.TryRemove(command.Username, out var _);

            return Confirmation.Ok(command);
        }
    }
}
