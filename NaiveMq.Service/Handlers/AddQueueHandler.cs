using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Service.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class AddQueueHandler : IHandler<AddQueue, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, AddQueue command)
        {
            context.CheckUser(context);

            var queueEnity = new QueueEntity
            {
                User = context.User.Username,
                Name = command.Name,
                Durable = command.Durable,
                Exchange = command.Exchange,
                Limit = command.Limit,
                LimitType = command.LimitType,
                LimitStrategy = command.LimitStrategy,
            };
            
            await ExecuteEntityAsync(context, queueEnity);

            return Confirmation.Ok(command);
        }

        public async Task ExecuteEntityAsync(ClientContext context, QueueEntity queueEnity)
        {
            var userQueues = context.Storage.GetUserQueues(context);

            var queue = new QueueCog(queueEnity);

            try
            {
                if (!userQueues.TryAdd(queueEnity.Name, queue))
                {
                    throw new ServerException(ErrorCode.QueueAlreadyExists, string.Format(ErrorCode.QueueAlreadyExists.GetDescription(), queueEnity.Name));
                }

                if (!context.Reinstate && queueEnity.Durable)
                {
                    await context.Storage.PersistentStorage.SaveQueueAsync(context.User.Username, queueEnity, context.CancellationToken);
                }
            }
            catch (ServerException)
            {
                throw;
            }
            catch
            {
                userQueues.TryRemove(queue.Entity.Name, out var _);
                queue.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
        }
    }
}
