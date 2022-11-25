using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class AddUserHandler : IHandler<AddUser, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(HandlerContext context, AddUser command)
        {
            if (!context.User.IsAdministrator)
            {
                throw new ServerException(ErrorCode.AccessDeniedAddingUser, ErrorCode.AccessDeniedAddingUser.GetDescription());
            }

            var userEntity = new UserEntity { Username = command.Username, PasswordHash = command.PasswordHash, HashAlgorithm = command.HashAlgorithm, IsAdministrator = command.IsAdministrator };

            try
            {
                if (!context.Storage.Users.TryAdd(command.Username, userEntity))
                {
                    throw new ServerException(ErrorCode.UserAlreadyExists, string.Format(ErrorCode.UserAlreadyExists.GetDescription(), command.Username));
                }

                context.Storage.UserQueues.TryAdd(command.Username, new());

                if (!context.Reinstate)
                {
                    if (context.Storage.PersistentStorage == null)
                    {
                        throw new ServerException(ErrorCode.CannotCreateDurableQueue, ErrorCode.CannotCreateDurableQueue.GetDescription());
                    }
                    else
                    {
                        await context.Storage.PersistentStorage.SaveUserAsync(userEntity, context.CancellationToken);
                    }
                }
            }
            catch
            {
                context.Storage.Users.TryRemove(userEntity.Username, out var _);
                throw;
            }

            return null;
        }

        public void Dispose()
        {
        }
    }
}
