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
                User = context.User.Entity.Username,
                Name = command.Name,
                Durable = command.Durable,
                Exchange = command.Exchange,
                LengthLimit = command.LengthLimit,
                VolumeLimit = command.VolumeLimit,
                LimitStrategy = command.LimitStrategy,
            };
            
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

        public void Dispose()
        {
        }
    }
}
