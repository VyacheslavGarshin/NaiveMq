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
            
            await ExecuteEntityAsync(context, userEntity, cancellationToken);

            return Confirmation.Ok(command);
        }

        public async Task ExecuteEntityAsync(ClientContext context, UserEntity userEntity, CancellationToken cancellationToken)
        {
            var user = new UserCog(userEntity, context.Storage.Counters, context.Storage.Service.SpeedCounterService);

            if (!context.Storage.Users.TryAdd(userEntity.Username, user))
            {
                throw new ServerException(ErrorCode.UserAlreadyExists, new[] { userEntity.Username });
            }

            try
            {
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
