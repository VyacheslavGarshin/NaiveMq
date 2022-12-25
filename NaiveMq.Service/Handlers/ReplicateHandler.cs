using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Commands;
using NaiveMq.Service.Enums;

namespace NaiveMq.Service.Handlers
{
    public class ReplicateHandler : AbstractHandler<Replicate, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, Replicate command, CancellationToken cancellationToken)
        {
            context.CheckClusterAdmin();

            if (context.Storage.Users.TryGetValue(command.User, out var user))
            {
                using var replicaContext = new ClientContext
                {
                    Storage = context.Storage,
                    Client = context.Client,
                    Logger = context.Logger,
                    User = user,
                    Mode = ClientContextMode.Replicate,
                };

                await context.Storage.Service.ExecuteCommandAsync(command.Request, replicaContext);
            }
            else
            {
                throw new ServerException(Client.ErrorCode.UserNotFound);
            };

            return Confirmation.Ok(command);
        }
    }
}
