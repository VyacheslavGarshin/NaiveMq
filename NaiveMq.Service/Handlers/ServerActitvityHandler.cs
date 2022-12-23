using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Commands;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class ServerActitvityHandler : AbstractHandler<ServerActitvity, Confirmation>
    {
        public override Task<Confirmation> ExecuteAsync(ClientContext context, ServerActitvity command, CancellationToken cancellationToken)
        {
            context.CheckClusterAdmin(context);

            var server = context.Storage.Cluster.Servers.Values.FirstOrDefault(x => x.Name == command.Name);

            if (server == null)
            {
                throw new ServerException(ErrorCode.ServerNotFound);
            }

            if (command.Start)
            {
                MarkOutdated(server);
            }

            if (command.ActiveQueues?.Count > 0)
            {
                SaveQueueStats(command, server);
            }

            if (command.Finish)
            {
                ClearOutdated(server);
            }

            return Task.FromResult(Confirmation.Ok(command));
        }

        private static void MarkOutdated(ClusterServer server)
        {
            foreach (var stat in server.ActiveQueues.Values)
            {
                stat.Outdated = true;
            }
        }

        private static void SaveQueueStats(ServerActitvity command, ClusterServer server)
        {
            foreach (var stat in command.ActiveQueues)
            {
                server.ActiveQueues.AddOrUpdate(stat.Key, stat, (key, value) => stat);
            }
        }

        private static void ClearOutdated(ClusterServer server)
        {
            server.ReplaceActiveQueues(server.ActiveQueues.Values.Where(x => !x.Outdated));
        }        
    }
}
