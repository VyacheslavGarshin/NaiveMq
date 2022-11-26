using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class DeleteQueueHandler : IHandler<DeleteQueue, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(HandlerContext context, DeleteQueue command)
        {
            context.CheckUser(context);

            var userQueues = context.Storage.GetUserQueues(context);

            if (userQueues.TryRemove(command.Name, out var queue))
            {
                try
                {
                    if (queue.Durable)
                    {
                        await context.Storage.PersistentStorage.DeleteQueueAsync(queue.User, queue.Name, context.CancellationToken);
                    }
                    
                    queue.Dispose();
                }
                catch
                {
                    userQueues.TryAdd(command.Name, queue);
                    throw;
                }
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), command.Name));
            }

            return null;
        }

        public void Dispose()
        {
        }
    }
}
