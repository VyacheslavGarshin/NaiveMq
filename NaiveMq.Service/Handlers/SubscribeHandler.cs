using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using System.Collections.Concurrent;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class SubscribeHandler : IHandler<Subscribe, Confirmation>
    {
        public Task<Confirmation> ExecuteAsync(ClientContext context, Subscribe command)
        {
            context.CheckUser(context);

            if (context.User.Queues.TryGetValue(command.Queue, out var queue))
            {
                if (!queue.Entity.Exchange)
                {
                    var subscription = new SubscriptionCog(context, queue, command.ConfirmMessage, command.ConfirmMessageTimeout);

                    if (context.Subscriptions.TryAdd(queue, subscription))
                    {
                        subscription.Start();
                    }
                    else
                    {
                        throw new ServerException(ErrorCode.SubscriptionAlreadyExists, string.Format(ErrorCode.SubscriptionAlreadyExists.GetDescription(), queue.Entity.Name));
                    }
                }
                else
                {
                    throw new ServerException(ErrorCode.SubscribeToExchange);
                }
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), command.Queue));
            }

            return Task.FromResult(Confirmation.Ok(command));
        }

        public void Dispose()
        {
        }
    }
}
