using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class UnsubscribeHandler : IHandler<Unsubscribe, Confirmation>
    {
        public Task<Confirmation> ExecuteAsync(HandlerContext context, Unsubscribe command)
        {
            if (context.Storage.Subscriptions.TryGetValue(context.Client, out var subscriptions))
            {
                if (context.Storage.Queues.TryGetValue(command.Queue, out var queue))
                {
                    if (subscriptions.TryRemove(queue, out var subscription))
                    {
                        subscription.Dispose();
                    }
                    else
                    {
                        throw new ServerException(ErrorCode.SubscriptionNotFound, string.Format(ErrorCode.SubscriptionNotFound.GetDescription(), command.Queue));
                    }
                }
                else
                {
                    throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), command.Queue));
                }
            }
            else
            {
                throw new ServerException(ErrorCode.SubscriptionNotFound, string.Format(ErrorCode.SubscriptionNotFound.GetDescription(), command.Queue));
            }

            return Task.FromResult((Confirmation)null);
        }

        public void Dispose()
        {
        }
    }
}
