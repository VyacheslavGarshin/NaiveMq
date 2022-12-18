using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Entities;
using NaiveMq.Service.Enums;

namespace NaiveMq.Service.Handlers
{
    public class MessageHandler : IHandler<Message, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, Message command)
        {
            context.CheckUser(context);

            var message = new MessageEntity
            {
                Id = command.Id,
                Tag = command.Tag,
                ClientId = context.Client?.Id,
                Queue = command.Queue,
                Request = command.Request,
                Persistent = command.Persistent,
                RoutingKey = command.RoutingKey,
                Data = command.Data.ToArray(), // materialize data from buffer
                DataLength = command.Data.Length,
            };

            return await ExecuteEntityAsync(context, message, command);
        }

        public async Task<Confirmation> ExecuteEntityAsync(ClientContext context, MessageEntity messageEntity, Message command = null)
        {
            if (context.User.Queues.TryGetValue(messageEntity.Queue, out var queue))
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

                CkeckMessage(messageEntity, queue, queues);

                if (await CheckLimitsAndDiscardAsync(context, queues, command))
                {
                    return Confirmation.Ok(command);
                }

                await EnqueueAsync(context, messageEntity, queues);

                if (messageEntity.Request)
                {
                    // confirmation will be redirected from subscriber to this client
                    return null;
                }
                else
                {
                    return Confirmation.Ok(command);
                }
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, new object[] { messageEntity.Queue });
            }
        }

        private static void CkeckMessage(MessageEntity message, QueueCog initialQueue, List<QueueCog> queues)
        {
            foreach (var queue in queues)
            {
                if (!initialQueue.Entity.Exchange && !queue.Entity.Durable && message.Persistent != Persistence.No)
                {
                    throw new ServerException(ErrorCode.PersistentMessageInNotDurableQueue, new object[] { queue.Entity.Name });
                }
            }
        }

        private async Task<bool> CheckLimitsAndDiscardAsync(ClientContext context, List<QueueCog> queues, Message command)
        {
            if (!context.Reinstate && command != null) {
                foreach (var queue in queues)
                {
                    if (context.Storage.MemoryLimitExceeded && queue.LengthLimit == null && queue.Length > 1)
                    {
                        var limit = (long)(queue.Length * (0.01 * context.Storage.Options.AutoQueueLimitPercent));

                        if (limit == 0)
                        {
                            limit = 1;
                        }

                        queue.LengthLimit = limit;
                    }

                    var limitType = queue.LimitExceeded(command.Data.Length);
                    if (limitType != LimitType.None)
                    {
                        switch (queue.Entity.LimitStrategy)
                        {
                            case LimitStrategy.Delay:
                                if (!await queue.WaitLimitSemaphoreAsync(command.ConfirmTimeout.Value, context.StoppingToken))
                                {
                                    // Client side will fire it's own confirmation timeout and abandon request. 
                                    // We need to discard the message.
                                    return true;
                                }

                                break;
                            case LimitStrategy.Reject:
                                switch (limitType)
                                {
                                    case LimitType.Length:
                                        throw new ServerException(ErrorCode.QueueLengthLimitExceeded, new object[] { queue.Entity.LengthLimit });
                                    case LimitType.Volume:
                                        throw new ServerException(ErrorCode.QueueVolumeLimitExceeded, new object[] { queue.Entity.VolumeLimit });
                                }

                                break;
                            case LimitStrategy.Discard:
                                return true;
                        }
                    }
                }
            }

            return false;
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

        private static async Task EnqueueAsync(ClientContext context, MessageEntity messageEntity, List<QueueCog> queues)
        {
            var entities = queues.Select(x => queues.Count == 1 ? messageEntity : messageEntity.Copy()).ToArray();

            for (var i = 0; i < queues.Count; i++)
            {
                queues[i].Enqueue(entities[i]);
            }

            if (!context.Reinstate && messageEntity.Persistent != Persistence.No)
            {
                for (var i = 0; i < queues.Count; i++)
                {
                    var queue = queues[i];
                    var entity = entities[i];

                    if (queue.Entity.Durable)
                    {
                        await context.Storage.PersistentStorage.SaveMessageAsync(context.User.Entity.Username, queue.Entity.Name, entity, context.StoppingToken);
                    }
                 
                    if (entity.Persistent == Persistence.DiskOnly)
                    {
                        entity.Data = null;
                    }
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
