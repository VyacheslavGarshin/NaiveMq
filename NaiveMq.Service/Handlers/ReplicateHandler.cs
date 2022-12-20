using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Commands;

namespace NaiveMq.Service.Handlers
{
    public class ReplicateHandler : AbstractHandler<Replicate, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, Replicate command)
        {
            context.CheckClusterAdmin(context);

            if (context.Storage.Users.TryGetValue(command.User, out var user))
            {
                var replicaContext = new ClientContext
                {
                    Client = context.Client,
                    Logger = context.Logger,
                    Reinstate = true,
                    StoppingToken = context.StoppingToken,
                    Storage = context.Storage,
                    User = user,
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
