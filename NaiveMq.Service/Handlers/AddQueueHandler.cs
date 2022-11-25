using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class AddQueueHandler : IHandler<AddQueue, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(HandlerContext context, AddQueue command)
        {
            var userQueues = context.Storage.GetUserQueues(context);

            var queue = new Queue(command.Name, command.Durable);

            try
            {
                if (!userQueues.TryAdd(command.Name, queue))
                {
                    throw new ServerException(ErrorCode.QueueAlreadyExists, string.Format(ErrorCode.QueueAlreadyExists.GetDescription(), command.Name));
                }

                if (!context.Reinstate && command.Durable)
                {
                    if (context.Storage.PersistentStorage == null)
                    {
                        throw new ServerException(ErrorCode.CannotCreateDurableQueue, ErrorCode.CannotCreateDurableQueue.GetDescription());
                    }
                    else
                    {
                        var queueEnity = new QueueEntity { User = context.User.Username, Name = queue.Name, Durable = queue.Durable };
                        await context.Storage.PersistentStorage.SaveQueueAsync(context.User.Username, queueEnity, context.CancellationToken);
                    }
                }
            }
            catch
            {
                userQueues.TryRemove(queue.Name, out var _);
                queue.Dispose();
                throw;
            }

            return null;
        }

        public void Dispose()
        {
        }
    }
}
