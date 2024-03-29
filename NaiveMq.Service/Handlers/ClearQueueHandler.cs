﻿using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Client.Enums;

namespace NaiveMq.Service.Handlers
{
    public class ClearQueueHandler : AbstractHandler<ClearQueue, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, ClearQueue command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            if (!context.User.Queues.TryGetValue(command.Name, out var queue))
            {
                throw new ServerException(ErrorCode.QueueNotFound, new[] { command.Name });
            }

            queue.SetStatus(QueueStatus.Clearing);

            if (queue.Entity.Durable)
            {
                await context.Storage.PersistentStorage.DeleteMessagesAsync(queue.Entity.User, queue.Entity.Name, cancellationToken);
            }

            queue.Clear();

            queue.SetStatus(QueueStatus.Started);

            return Confirmation.Ok(command);
        }
    }
}
