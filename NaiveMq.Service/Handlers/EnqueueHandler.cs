using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class EnqueueHandler : IHandler<Enqueue, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(HandlerContext context, Enqueue command)
        {
            if (context.Storage.Queues.TryGetValue(command.Queue, out var queue))
            {
                var message = new MessageEntity { Id = command.Id.Value, Queue = command.Queue, Text = command.Text };

                queue.Enqueue(message);

                if (!context.Reinstate && queue.Durable)
                {
                    await context.Storage.PersistentStorage.SaveMessageAsync(context.User, command.Queue, message, context.CancellationToken);
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
