using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Commands;
using NaiveMq.Service.Enums;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class ReplicateHandler : AbstractHandler<Replicate, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, Replicate command, CancellationToken cancellationToken)
        {
            context.CheckClusterAdmin();

            if (!context.Storage.Users.TryGetValue(command.User, out var user))
            {
                throw new ServerException(ErrorCode.UserNotFound, new[] { command.User });
            };

            using var replicaContext = new ClientContext
            {
                Storage = context.Storage,
                Client = context.Client,
                Logger = context.Logger,
                User = user,
                Mode = ClientContextMode.Replicate,
            };

            await context.Storage.Service.ExecuteCommandAsync(command.Request, replicaContext);

            return Confirmation.Ok(command);
        }
    }
}
