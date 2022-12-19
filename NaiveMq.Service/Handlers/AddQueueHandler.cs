using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Service.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class AddQueueHandler : AbstractHandler<AddQueue, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, AddQueue command)
        {
            context.CheckUser(context);

            var queueEnity = QueueEntity.FromCommand(command);
            queueEnity.User = context.User.Entity.Username;

            await ExecuteEntityAsync(context, queueEnity);

            return Confirmation.Ok(command);
        }

        public async Task ExecuteEntityAsync(ClientContext context, QueueEntity queueEntity)
        {
            var queue = new QueueCog(queueEntity);

            try
            {
                if (!context.User.Queues.TryAdd(queueEntity.Name, queue))
                {
                    throw new ServerException(ErrorCode.QueueAlreadyExists, new object[] { queueEntity.Name });
                }

                if (!context.Reinstate && queueEntity.Durable)
                {
                    await context.Storage.PersistentStorage.SaveQueueAsync(context.User.Entity.Username, queueEntity, context.StoppingToken);
                }
            }
            catch (ServerException)
            {
                throw;
            }
            catch
            {
                context.User.Queues.TryRemove(queue.Entity.Name, out var _);
                queue.Dispose();
                throw;
            }
        }
    }
}
