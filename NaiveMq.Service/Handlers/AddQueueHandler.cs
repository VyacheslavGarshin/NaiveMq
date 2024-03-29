﻿using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Entities;
using NaiveMq.Client;
using NaiveMq.Service.Enums;
using NaiveMq.Client.Enums;

namespace NaiveMq.Service.Handlers
{
    public class AddQueueHandler : AbstractHandler<AddQueue, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, AddQueue command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            var queueEnity = QueueEntity.FromCommand(command);
            queueEnity.User = context.User.Entity.Username;

            await ExecuteEntityAsync(context, queueEnity, command, cancellationToken);

            return Confirmation.Ok(command);
        }

        public static async Task ExecuteEntityAsync(ClientContext context, QueueEntity queueEntity, AddQueue command, CancellationToken cancellationToken)
        {
            var queue = new QueueCog(queueEntity, context.User, context.Storage.Service.SpeedCounterService);

            try
            {
                if (!context.User.Queues.TryAdd(queueEntity.Name, queue))
                {
                    if (command != null && command.Try)
                    {
                        return;
                    }

                    throw new ServerException(ErrorCode.QueueAlreadyExists, new[] { queueEntity.Name });
                }

                if (context.Mode == ClientContextMode.Client && queueEntity.Durable)
                {
                    await context.Storage.PersistentStorage.SaveQueueAsync(context.User.Entity.Username, queueEntity, cancellationToken);
                }

                queue.SetStatus(QueueStatus.Started);
            }
            catch
            {
                context.User.Queues.TryRemove(queue.Entity.Name, out var _);
                queue.Dispose();
                throw;
            }
        }
    }
}
