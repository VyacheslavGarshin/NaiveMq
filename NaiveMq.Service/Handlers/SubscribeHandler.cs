﻿using NaiveMq.Service.Cogs;
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

            if (!queues.TryGetValue(command.Queue, out var queue))
            {
                throw new ServerException(ErrorCode.QueueNotFound, new[] { command.Queue });
            }

            if (queue.Entity.Exchange)
            {
                throw new ServerException(ErrorCode.SubscribeToExchange);
            }

            var subscription = new SubscriptionCog(
                    context.Client,
                    queue,
                    command.ConfirmMessage,
                    command.ConfirmMessageTimeout,
                    command.ClusterStrategy,
                    command.ClusterIdleTimout);

            if (!context.Subscriptions.TryAdd(queue, subscription))
            {
                subscription.Dispose();
                throw new ServerException(ErrorCode.SubscriptionAlreadyExists, new[] { queue.Entity.Name });
            }

            subscription.Start();

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
                    throw new ClientException(ErrorCode.UserNotFound, new[] { command.User });
                }

                queues = user.Queues;
            }

            return queues;
        }
    }
}
