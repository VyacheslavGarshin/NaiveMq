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
            if (!context.Reinstate)
            {
                context.CheckAdmin(context);
            }

            var userEntity = new UserEntity
            {
                Username = command.Username,
                PasswordHash = context.Reinstate ? command.Password : command.Password.ComputeHash(),
                IsAdministrator = command.IsAdministrator
            };

            try
            {
                if (!context.Storage.Users.TryAdd(command.Username, userEntity))
                {
                    throw new ServerException(ErrorCode.UserAlreadyExists, string.Format(ErrorCode.UserAlreadyExists.GetDescription(), command.Username));
                }

                if (!context.Reinstate)
                {
                    await context.Storage.PersistentStorage.SaveUserAsync(userEntity, context.CancellationToken);
                }

                context.Storage.UserQueues.TryAdd(command.Username, new());
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
