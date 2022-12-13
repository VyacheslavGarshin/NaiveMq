using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Service.Entities;
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

            if (string.IsNullOrEmpty(command.Password))
            {
                throw new ServerException(ErrorCode.PasswordEmpty);
            }

            var userEntity = new UserEntity
            {
                Username = command.Username,
                PasswordHash = command.Password.ComputeHash(),
                Administrator = command.Administrator
            };
            
            await ExecuteEntityAsync(context, userEntity);

            return Confirmation.Ok(command);
        }

        public async Task ExecuteEntityAsync(ClientContext context, UserEntity userEntity)
        {
            if (!context.Storage.Users.TryAdd(userEntity.Username, new UserCog(userEntity)))
            {
                throw new ServerException(ErrorCode.UserAlreadyExists, new object[] { userEntity.Username });
            }

            if (!context.Reinstate)
            {
                try
                {

                    await context.Storage.PersistentStorage.SaveUserAsync(userEntity, context.StoppingToken);
                }
                catch
                {
                    context.Storage.Users.TryRemove(userEntity.Username, out var _);
                    throw;
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
