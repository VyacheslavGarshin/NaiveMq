using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Handlers
{
    public class MessageHandler : IHandler<Message, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, Message command)
        {
            context.CheckUser(context);

            var userQueues = context.Storage.GetUserQueues(context);

            if (userQueues.TryGetValue(command.Queue, out var queue))
            {
                var queues = new List<Queue>();

                if (queue.Exchange)
                {
                    queues.AddRange(MatchBoundQueues(context, command, userQueues, queue));

                    if (!queues.Any())
                    {
                        throw new ServerException(ErrorCode.ExchangeCannotRouteMessage, ErrorCode.ExchangeCannotRouteMessage.GetDescription());
                    }
                }
                else
                {
                    queues.Add(queue);
                }

                if (command.Request)
                {
                    command.Durable = false;
                }

                await Enqueue(context, command, queues);

                if (command.Request)
                {
                    context.Storage.ClientRequests.AddRequest(context.Client.Id, command.Id, command.ConfirmTimeout);

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
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), command.Queue));
            }
        }

        private static List<Queue> MatchBoundQueues(ClientContext context, Message command, ConcurrentDictionary<string, Queue> userQueues, Queue exchange)
        {
            var result = new List<Queue>();
            var userBindings = context.Storage.GetUserBindings(context);

            if (userBindings.TryGetValue(exchange.Name, out var bindings))
            {
                foreach (var binding in bindings)
                {
                    if ((binding.Value.Regex == null || binding.Value.Regex.IsMatch(command.BindingKey))
                        && userQueues.TryGetValue(binding.Value.Queue, out var boundQueue))
                    {
                        result.Add(boundQueue);
                    }
                }
            }

            return result;
        }

        private static async Task Enqueue(ClientContext context, Message command, List<Queue> queues)
        {
            foreach (var queue in queues)
            {
                var message = new MessageEntity
                {
                    Id = command.Id,
                    Queue = queue.Name,
                    Request = command.Request,
                    Durable = command.Durable && queue.Durable,
                    BindingKey = command.BindingKey,
                    Text = command.Text
                };

                queue.Enqueue(message);

                if (!context.Reinstate && message.Durable)
                {
                    await context.Storage.PersistentStorage.SaveMessageAsync(context.User.Username, message.Queue, message, context.CancellationToken);
                }

                queue.ReleaseDequeue();
            }
        }

        public void Dispose()
        {
        }
    }
}
