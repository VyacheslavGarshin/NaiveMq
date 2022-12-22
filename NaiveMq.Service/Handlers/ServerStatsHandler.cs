using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Commands;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class ServerStatsHandler : AbstractHandler<ServerStats, Confirmation>
    {
        public override Task<Confirmation> ExecuteAsync(ClientContext context, ServerStats command)
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

            if (command.QueueStats?.Count > 0)
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
            foreach (var stat in server.UserQueueStats.Values)
            {
                stat.Outdated = true;
            }
        }

        private static void SaveQueueStats(ServerStats command, ClusterServer server)
        {
            foreach (var stat in command.QueueStats)
            {
                server.UserQueueStats.AddOrUpdate(stat.Key, stat, (key, value) => stat);
            }
        }

        private static void ClearOutdated(ClusterServer server)
        {
            server.ReplaceUserQueueStats(server.UserQueueStats.Values.Where(x => !x.Outdated));
        }        
    }
}
