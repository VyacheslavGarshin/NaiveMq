using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class UnsubscribeHandler : AbstractHandler<Unsubscribe, Confirmation>
    {
        public override Task<Confirmation> ExecuteAsync(ClientContext context, Unsubscribe command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            if (!context.User.Queues.TryGetValue(command.Queue, out var queue))
            {
                throw new ServerException(ErrorCode.QueueNotFound, new[] { command.Queue });
            }

            if (!context.Subscriptions.TryRemove(queue, out var subscription))
            {
                throw new ServerException(ErrorCode.SubscriptionNotFound, new[] { command.Queue });
            }

            subscription.Dispose();

            return Task.FromResult(Confirmation.Ok(command));
        }
    }
}
