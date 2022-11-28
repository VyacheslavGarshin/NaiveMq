using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class AddUserHandler : IHandler<AddUser, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, AddUser command)
        {
            if (!context.Reinstate && context.Storage.Users.Any())
            {
                context.CheckAdmin(context);
            }

            var userEntity = new UserEntity
            {
                Username = command.Username,
                PasswordHash = context.Reinstate ? command.Password : command.Password.ComputeHash(),
                Administrator = command.Administrator
            };

            if (!context.Storage.Users.TryAdd(command.Username, userEntity))
            {
                throw new ServerException(ErrorCode.UserAlreadyExists, string.Format(ErrorCode.UserAlreadyExists.GetDescription(), command.Username));
            }

            if (!context.Reinstate)
            {
                try
                {

                    await context.Storage.PersistentStorage.SaveUserAsync(userEntity, context.CancellationToken);
                }
                catch
                {
                    context.Storage.Users.TryRemove(userEntity.Username, out var _);
                    throw;
                }
            }

            context.Storage.UserQueues.TryAdd(command.Username, new(StringComparer.InvariantCultureIgnoreCase));
            context.Storage.UserBindings.TryAdd(command.Username, new(StringComparer.InvariantCultureIgnoreCase));

            return null;
        }

        public void Dispose()
        {
        }
    }
}
