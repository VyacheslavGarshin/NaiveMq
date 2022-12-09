using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;
using System.Collections.Concurrent;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Entities;

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
                ClientId = context.Client?.Id,
                Queue = command.Queue,
                Request = command.Request,
                Persistent = command.Persistent,
                RoutingKey = command.RoutingKey,
                Data = command.Data,
                DataLength = command.Data?.Length ?? 0,
            };

            return await ExecuteEntityAsync(context, message, command);
        }

        public async Task<Confirmation> ExecuteEntityAsync(ClientContext context, MessageEntity message, Message command = null)
        {
            var userQueues = context.Storage.GetUserQueues(context);

            if (userQueues.TryGetValue(message.Queue, out var queue))
            {
                if (!queue.Entity.Durable && message.Persistent != Persistent.No)
                {
                    throw new ServerException(ErrorCode.CannotEnqueuePersistentMessageInNotDurableQueue);
                }

                var queues = new List<QueueCog>();

                if (queue.Entity.Exchange)
                {
                    queues.AddRange(MatchBoundQueues(context, message, userQueues, queue));

                    if (!queues.Any())
                    {
                        throw new ServerException(ErrorCode.ExchangeCannotRouteMessage);
                    }
                }
                else
                {
                    queues.Add(queue);
                }

                if (message.Request)
                {
                    message.Persistent = Persistent.No;
                }

                await Enqueue(context, message, queues);

                if (message.Request)
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
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), message.Queue));
            }
        }

        private static List<QueueCog> MatchBoundQueues(ClientContext context, MessageEntity message, ConcurrentDictionary<string, QueueCog> userQueues, QueueCog exchange)
        {
            var result = new List<QueueCog>();
            var userBindings = context.Storage.GetUserBindings(context);

            if (userBindings.TryGetValue(exchange.Entity.Name, out var bindings))
            {
                foreach (var binding in bindings)
                {
                    if ((binding.Value.Pattern == null || binding.Value.Pattern.IsMatch(message.RoutingKey))
                        && userQueues.TryGetValue(binding.Value.Entity.Queue, out var boundQueue))
                    {
                        result.Add(boundQueue);
                    }
                }
            }

            return result;
        }

        private static async Task Enqueue(ClientContext context, MessageEntity message, List<QueueCog> queues)
        {
            foreach (var queue in queues)
            {
                queue.Enqueue(message);

                if (!context.Reinstate && message.Persistent != Persistent.No)
                {
                    await context.Storage.PersistentStorage.SaveMessageAsync(context.User.Username, message.Queue, message, context.CancellationToken);
                }

                if (message.Persistent == Persistent.DiskOnly)
                {
                    message.Data = null;
                }

                queue.ReleaseDequeue();
            }
        }

        public void Dispose()
        {
        }
    }
}
