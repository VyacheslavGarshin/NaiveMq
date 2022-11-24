using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class DequeueHandler : IHandler<Dequeue, DequeueResponse>
    {
        public async Task<DequeueResponse> ExecuteAsync(HandlerContext context, Dequeue command)
        {
            if (context.Storage.Queues.TryGetValue(command.Queue, out var queue))
            {
                if (queue.TryDequeue(out var message))
                {
                    if (queue.Durable)
                    {
                        await context.Storage.PersistentStorage.DeleteMessageAsync(context.User, queue.Name, message.Id, context.CancellationToken);
                    }

                    return new DequeueResponse { Message = message };
                }
                else
                {
                    throw new ServerException(ErrorCode.QueueIsEmpty, string.Format(ErrorCode.QueueIsEmpty.GetDescription(), queue.Name));
                }
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), command.Queue));
            }
        }

        public void Dispose()
        {
        }
    }
}
