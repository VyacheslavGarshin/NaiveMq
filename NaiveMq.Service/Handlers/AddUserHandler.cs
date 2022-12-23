using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class AddUserHandler : AbstractHandler<AddUser, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, AddUser command, CancellationToken cancellationToken)
        {
            if (!context.Reinstate && context.Storage.Users.Any())
            {
                context.CheckAdmin(context);
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
            var userCog = new UserCog(userEntity, context.Storage.Counters, context.Storage.Service.SpeedCounterService);

            try
            {
                if (!context.Storage.Users.TryAdd(userEntity.Username, userCog))
                {
                    throw new ServerException(ErrorCode.UserAlreadyExists, new object[] { userEntity.Username });
                }

                if (!context.Reinstate)
                {
                    await context.Storage.PersistentStorage.SaveUserAsync(userEntity, cancellationToken);
                }
            }
            catch
            {
                context.Storage.Users.TryRemove(userEntity.Username, out var _);
                userCog.Dispose();
                throw;
            }
        }
    }
}
