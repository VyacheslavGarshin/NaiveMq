using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Service.Entities;
using NaiveMq.Client;
using Newtonsoft.Json;

namespace NaiveMq.Service.Handlers
{
    public class UpdateUserHandler : AbstractHandler<UpdateUser, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, UpdateUser command, CancellationToken cancellationToken)
        {
            context.CheckAdmin();

            UserCog user = null;
            UserEntity oldEntity = null;

            try
            {
                if (!context.Storage.Users.TryGetValue(command.Username, out user))
                {
                    throw new ServerException(ErrorCode.UserNotFound, new[] { command.Username });
                }

                oldEntity = JsonConvert.DeserializeObject<UserEntity>(JsonConvert.SerializeObject(user.Entity));

                if (string.Equals(context.User.Entity.Username, command.Username, StringComparison.InvariantCultureIgnoreCase)
                    && !command.Administrator)
                {
                    throw new ServerException(ErrorCode.UserUnsetAdministratorSelf);
                }

                user.Entity.Administrator = command.Administrator;

                if (!string.IsNullOrEmpty(command.Password))
                {
                    user.Entity.PasswordHash = command.Password.ComputeHash();
                }

                await context.Storage.PersistentStorage.SaveUserAsync(user.Entity, cancellationToken);
            }
            catch
            {
                if (user != null && oldEntity != null)
                {
                    user.Entity.Administrator = oldEntity.Administrator;
                    user.Entity.PasswordHash = oldEntity.PasswordHash;
                }

                throw;
            }

            return Confirmation.Ok(command);
        }
    }
}
