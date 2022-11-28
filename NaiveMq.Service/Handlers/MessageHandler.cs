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

            if (userQueues.TryGetValue(command.Queue, out var queue))
            {
                var message = new MessageEntity { Id = command.Id.Value, Queue = command.Queue, Durable = command.Durable, Text = command.Text };

                queue.Enqueue(message);

                if (!context.Reinstate && message.Durable)
                {
                    await context.Storage.PersistentStorage.SaveMessageAsync(context.User.Username, message.Queue, message, context.CancellationToken);
                }

                queue.ReleaseDequeue();
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), command.Queue));
            }

            return null;
        }

        public void Dispose()
        {
        }
    }
}
