using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Entities;
using NaiveMq.Client;
using NaiveMq.Service.Enums;
using NaiveMq.Client.Enums;

namespace NaiveMq.Service.Handlers
{
    public class AddUserHandler : AbstractHandler<AddUser, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, AddUser command, CancellationToken cancellationToken)
        {
            if (context.Mode == ClientContextMode.Client)
            {
                context.CheckAdmin();
            }

            if (string.IsNullOrEmpty(command.Password))
            {
                throw new ServerException(ErrorCode.PasswordEmpty);
            }

            var userEntity = UserEntity.FromCommand(command);
            
            await ExecuteEntityAsync(context, userEntity, command, cancellationToken);

            return Confirmation.Ok(command);
        }

        public static async Task ExecuteEntityAsync(ClientContext context, UserEntity userEntity, AddUser command, CancellationToken cancellationToken)
        {
            var user = new UserCog(userEntity, context.Storage.Counters, context.Storage.Service.SpeedCounterService);

            try
            {
                if (!context.Storage.Users.TryAdd(userEntity.Username, user))
                {
                    if (command != null && command.Try)
                    {
                        return;
                    }

                    throw new ServerException(ErrorCode.UserAlreadyExists, new[] { userEntity.Username });
                }

                if (context.Mode == ClientContextMode.Client || context.Mode == ClientContextMode.Init)
                {
                    await context.Storage.PersistentStorage.SaveUserAsync(userEntity, cancellationToken);
                }

                user.SetStatus(UserStatus.Started);
            }
            catch
            {
                context.Storage.Users.TryRemove(userEntity.Username, out var _);
                user.Dispose();
                throw;
            }
        }
    }
}
