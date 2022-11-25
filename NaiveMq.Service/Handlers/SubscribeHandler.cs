using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using System.Collections.Concurrent;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class SubscribeHandler : IHandler<Subscribe, Confirmation>
    {
        public Task<Confirmation> ExecuteAsync(HandlerContext context, Subscribe command)
        {
            if (context.Storage.Queues.TryGetValue(command.Queue, out var queue))
            {
                var subscriptions = context.Storage.Subscriptions.GetOrAdd(context.Client, (key) => new ConcurrentDictionary<Queue, Subscription>());
                var subscription = new Subscription(context, queue, command.ClientConfirm, command.ClientConfirmTimeout);

                if (subscriptions.TryAdd(queue, subscription))
                {
                    subscription.Start();
                }
                else
                {
                    throw new ServerException(ErrorCode.SubscriptionAlreadyExists, string.Format(ErrorCode.SubscriptionAlreadyExists.GetDescription(), queue.Name));
                }
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), command.Queue));
            }

            return Task.FromResult((Confirmation)null);
        }

        public void Dispose()
        {
        }
    }
}
