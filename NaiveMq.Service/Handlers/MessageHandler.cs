﻿using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Entities;
using NaiveMq.Service.Enums;

namespace NaiveMq.Service.Handlers
{
    public class MessageHandler : AbstractHandler<Message, MessageResponse>
    {
        public override async Task<MessageResponse> ExecuteAsync(ClientContext context, Message command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            var message = MessageEntity.FromCommand(command, context.Client.Id);

            return await ExecuteEntityAsync(context, command.Queue, message, command, cancellationToken);
        }

        public async Task<MessageResponse> ExecuteEntityAsync(ClientContext context, string queueName, MessageEntity messageEntity, Message command, CancellationToken cancellationToken)
        {
            var queue = FindQueue(context, queueName);

            if (queue != null)
            {
                var targetQueues = FindTargetQueues(context, messageEntity, queue);

                CkeckMessage(messageEntity, queue, targetQueues);

                foreach (var limitedQueue in GetLimitedQueues(context, targetQueues, command))
                {
                    switch (limitedQueue.Queue.Entity.LimitStrategy)
                    {
                        case LimitStrategy.Delay:
                            if (!await limitedQueue.Queue.WaitLimitSemaphoreAsync(command.ConfirmTimeout.Value, cancellationToken))
                            {
                                // Client side will fire it's own confirmation timeout and abandon request. 
                                // We need to discard the message.
                                return MessageResponse.Ok(command);
                            }

                            break;
                        case LimitStrategy.Reject:
                            switch (limitedQueue.LimitType)
                            {
                                case LimitType.Length:
                                    throw new ServerException(ErrorCode.QueueLengthLimitExceeded, new object[] { limitedQueue.Queue.Entity.LengthLimit });
                                case LimitType.Volume:
                                    throw new ServerException(ErrorCode.QueueVolumeLimitExceeded, new object[] { limitedQueue.Queue.Entity.VolumeLimit });
                            }

                            break;
                        case LimitStrategy.Discard:
                            return MessageResponse.Ok(command);
                    }                   
                }

                var messageEntitiesToSave = EnqueueAndGetEntitiesToSave(context, messageEntity, targetQueues);

                if (messageEntitiesToSave.Any())
                {
                    await SaveAsync(context, messageEntitiesToSave, cancellationToken);
                }

                if (messageEntity.Request)
                {
                    // confirmation will be redirected from subscriber to this client
                    return null;
                }
                else
                {
                    return MessageResponse.Ok(command);
                }
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, new[] { queueName });
            }
        }

        private static QueueCog FindQueue(ClientContext context, string queueName)
        {
            QueueCog queue = null;

            if (context.LastQueue != null)
            {
                // non blocking check last queue the same
                if (context.LastQueue.Entity.Name.Equals(queueName, StringComparison.InvariantCultureIgnoreCase))
                {
                    queue = context.LastQueue;

                    if (!queue.Entity.Name.Equals(queueName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        queue = null;
                    }
                }
            }

            if (queue == null && context.User.Queues.TryGetValue(queueName, out queue))
            {
                context.LastQueue = queue;
            }

            return queue;
        }

        private static List<QueueCog> FindTargetQueues(ClientContext context, MessageEntity messageEntity, QueueCog queue)
        {
            var queues = new List<QueueCog>();

            if (queue.Entity.Exchange)
            {
                queues.AddRange(MatchBoundQueues(context, messageEntity, queue));

                if (!queues.Any())
                {
                    throw new ServerException(ErrorCode.ExchangeCannotRouteMessage);
                }
            }
            else
            {
                queues.Add(queue);
            }

            return queues;
        }

        private static void CkeckMessage(MessageEntity message, QueueCog initialQueue, List<QueueCog> targetQueues)
        {
            if (!initialQueue.Entity.Exchange)
            {
                foreach (var queue in targetQueues)
                {
                    if (!queue.Entity.Durable && message.Persistent != Persistence.No)
                    {
                        throw new ServerException(ErrorCode.PersistentMessageInNotDurableQueue, new[] { queue.Entity.Name });
                    }
                }
            }
        }

        private static List<LimitedQueue> GetLimitedQueues(ClientContext context, List<QueueCog> queues, Message command)
        {
            var result = new List<LimitedQueue>();

            if (context.Mode == ClientContextMode.Client && command != null)
            {
                foreach (var queue in queues)
                {
                    SetForcedQueueLimit(context, queue);

                    var limitType = queue.LimitExceeded(command.Data.Length);

                    if (limitType != LimitType.None)
                    {
                        result.Add(new LimitedQueue { Queue = queue, LimitType = limitType });
                    }
                }
            }

            return result;
        }

        private static void SetForcedQueueLimit(ClientContext context, QueueCog queue)
        {
            if (context.Storage.MemoryLimitExceeded && queue.ForcedLengthLimit == null && queue.Length > 1)
            {
                var limit = (long)(queue.Length * (0.01 * context.Storage.Service.Options.AutoQueueLimitPercent));

                if (limit == 0)
                {
                    limit = 1;
                }

                queue.ForcedLengthLimit = limit;
            }
        }

        private static List<QueueCog> MatchBoundQueues(ClientContext context, MessageEntity message, QueueCog exchange)
        {
            var result = new List<QueueCog>();

            if (context.User.Bindings.TryGetValue(exchange.Entity.Name, out var bindings))
            {
                foreach (var binding in bindings)
                {
                    if ((binding.Value.Pattern == null || binding.Value.Pattern.IsMatch(message.RoutingKey))
                        && context.User.Queues.TryGetValue(binding.Value.Entity.Queue, out var boundQueue))
                    {
                        result.Add(boundQueue);
                    }
                }
            }

            return result;
        }

        private static List<QueueAndMessage> EnqueueAndGetEntitiesToSave(ClientContext context, MessageEntity messageEntity, List<QueueCog> queues)
        {
            var result = new List<QueueAndMessage>();

            var entities = queues.Select(x => queues.Count == 1 ? messageEntity : messageEntity.Copy()).ToArray();

            for (var i = 0; i < queues.Count; i++)
            {
                queues[i].Enqueue(entities[i]);
            }

            if (context.Mode == ClientContextMode.Client && messageEntity.Persistent != Persistence.No)
            {
                for (var i = 0; i < queues.Count; i++)
                {
                    var queue = queues[i];
                    var entity = entities[i];

                    if (queue.Entity.Durable)
                    {
                        result.Add(new QueueAndMessage { Queue = queue, Message = entity });
                    }
                }
            }

            return result;
        }

        private static async Task SaveAsync(ClientContext context, List<QueueAndMessage> queueAndMessages, CancellationToken cancellationToken)
        {
            foreach (var queueAndMessage in queueAndMessages)
            {
                await context.Storage.PersistentStorage.SaveMessageAsync(
                    context.User.Entity.Username,
                    queueAndMessage.Queue.Entity.Name,
                    queueAndMessage.Message,
                    cancellationToken);

                if (queueAndMessage.Message.Persistent == Persistence.DiskOnly)
                {
                    queueAndMessage.Message.Data = null;
                }
            }
        }

        private class QueueAndMessage
        {
            public QueueCog Queue { get; set; }

            public MessageEntity Message { get; set; }
        }

        private class LimitedQueue
        {
            public QueueCog Queue { get; set; }

            public LimitType LimitType { get; set; }
        }
    }
}
