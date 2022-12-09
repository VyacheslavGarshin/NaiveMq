using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class DeleteQueueHandler : IHandler<DeleteQueue, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, DeleteQueue command)
        {
            context.CheckUser(context);

            var userQueues = context.Storage.GetUserQueues(context);

            if (userQueues.TryRemove(command.Name, out var queue))
            {
                try
                {
                    if (queue.Entity.Durable)
                    {
                        await context.Storage.PersistentStorage.DeleteQueueAsync(queue.Entity.User, queue.Entity.Name, context.CancellationToken);
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

            return Confirmation.Ok(command);
        }

        public void Dispose()
        {
        }
    }
}
