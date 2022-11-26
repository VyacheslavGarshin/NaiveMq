using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class DeleteUserHandler : IHandler<DeleteUser, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(HandlerContext context, DeleteUser command)
        {
            context.CheckAdmin(context);

            UserEntity userEntity = null;

            try
            {
                if (!context.Storage.Users.TryRemove(command.Username, out userEntity))
                {
                    throw new ServerException(ErrorCode.UserNotFound, string.Format(ErrorCode.UserNotFound.GetDescription(), command.Username));
                }

                if (string.Equals(context.User.Username, command.Username, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new ServerException(ErrorCode.UserCannotDeleteSelf, ErrorCode.UserCannotDeleteSelf.GetDescription());
                }

                if (!context.Reinstate)
                {
                    await context.Storage.PersistentStorage.DeleteUserAsync(command.Username, context.CancellationToken);
                }

                context.Storage.UserQueues.TryRemove(command.Username, out var userQueues);

                foreach (var queue in userQueues.Values)
                {
                    queue.Dispose();
                }

                userQueues.Clear();
            }
            catch
            {
                context.Storage.Users.TryAdd(command.Username, userEntity);
                throw;
            }

            return null;
        }

        public void Dispose()
        {
        }
    }
}
