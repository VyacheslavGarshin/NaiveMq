using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class MessageHandler : IHandler<Message, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, Message command)
        {
            context.CheckUser(context);

            var userQueues = context.Storage.GetUserQueues(context);

            if (userQueues.TryGetValue(command.Queue, out var initialQueue))
            {
                var queues = new List<Queue>();

                if (initialQueue.Exchange)
                {
                    var userBindings = context.Storage.GetUserBindings(context);

                    if (userBindings.TryGetValue(initialQueue.Name, out var bindings))
                    {
                        foreach (var binding in bindings)
                        {
                            if ((binding.Value.Regex == null || binding.Value.Regex.IsMatch(command.BindingKey))
                                && userQueues.TryGetValue(binding.Value.Queue, out var boundQueue))
                            {
                                queues.Add(boundQueue);
                            }
                        }
                    }

                    if (!queues.Any())
                    {
                        throw new ServerException(ErrorCode.ExchangeCannotRouteMessage, ErrorCode.ExchangeCannotRouteMessage.GetDescription());
                    }
                }
                else
                {
                    queues.Add(initialQueue);
                }

                await Enqueue(context, command, queues);
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), command.Queue));
            }

            return null;
        }

        private static async Task Enqueue(ClientContext context, Message command, List<Queue> queues)
        {
            foreach (var queue in queues)
            {
                var message = new MessageEntity
                {
                    Id = command.Id.Value,
                    Queue = queue.Name,
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
