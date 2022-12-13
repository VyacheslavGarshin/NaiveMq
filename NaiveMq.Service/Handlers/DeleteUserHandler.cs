using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class DeleteUserHandler : IHandler<DeleteUser, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, DeleteUser command)
        {
            context.CheckAdmin(context);

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

                await context.Storage.PersistentStorage.DeleteUserAsync(command.Username, context.StoppingToken);

                user.Dispose();
            }
            catch
            {
                context.Storage.Users.TryAdd(command.Username, user);
                throw;
            }

            return Confirmation.Ok(command);
        }

        public void Dispose()
        {
        }
    }
}
