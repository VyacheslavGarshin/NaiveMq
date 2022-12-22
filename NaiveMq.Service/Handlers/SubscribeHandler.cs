using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class SubscribeHandler : AbstractHandler<Subscribe, Confirmation>
    {
        public override Task<Confirmation> ExecuteAsync(ClientContext context, Subscribe command)
        {
            context.CheckUser(context);

            if (context.User.Queues.TryGetValue(command.Queue, out var queue))
            {
                if (!queue.Entity.Exchange)
                {
                    var subscription = new SubscriptionCog(context, queue, command.ConfirmMessage, command.ConfirmMessageTimeout, command.ClusterStrategy);

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
    }
}
