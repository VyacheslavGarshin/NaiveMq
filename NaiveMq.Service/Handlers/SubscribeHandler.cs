using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Handlers
{
    public class SubscribeHandler : AbstractHandler<Subscribe, Confirmation>
    {
        public override Task<Confirmation> ExecuteAsync(ClientContext context, Subscribe command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            var queues = FindQueues(context, command);

            if (queues.TryGetValue(command.Queue, out var queue))
            {
                if (!queue.Entity.Exchange)
                {
                    var subscription = new SubscriptionCog(
                        context,
                        queue,
                        command.ConfirmMessage,
                        command.ConfirmMessageTimeout,
                        command.ClusterStrategy,
                        command.ClusterIdleTimout);

                    if (context.Subscriptions.TryAdd(queue, subscription))
                    {
                        subscription.Start();
                    }
                    else
                    {
                        subscription.Dispose();
                        throw new ServerException(ErrorCode.SubscriptionAlreadyExists, new object[] { queue.Entity.Name });
                    }
                }
                else
                {
                    throw new ServerException(ErrorCode.SubscribeToExchange);
                }
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, new object[] { command.Queue });
            }

            return Task.FromResult(Confirmation.Ok(command));
        }

        private static ConcurrentDictionary<string, QueueCog> FindQueues(ClientContext context, Subscribe command)
        {
            var queues = context.User.Queues;

            if (!string.IsNullOrEmpty(command.User))
            {
                context.CheckAdmin();

                if (!context.Storage.Users.TryGetValue(command.User, out var user))
                {
                    queues = user.Queues;
                }
                else
                {
                    throw new ClientException(ErrorCode.UserNotFound, new object[] { command.User });
                }
            }

            return queues;
        }
    }
}
