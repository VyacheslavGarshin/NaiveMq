using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class AddQueueHandler : IHandler<AddQueue, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, AddQueue command)
        {
            context.CheckUser(context);

            var userQueues = context.Storage.GetUserQueues(context);

            var queue = new Queue(command.Name, context.User.Username, command.Durable, command.Exchange);

            try
            {
                if (!userQueues.TryAdd(command.Name, queue))
                {
                    throw new ServerException(ErrorCode.QueueAlreadyExists, string.Format(ErrorCode.QueueAlreadyExists.GetDescription(), command.Name));
                }

                if (!context.Reinstate && command.Durable)
                {
                    var queueEnity = new QueueEntity { User = queue.User, Name = queue.Name, Durable = queue.Durable, Exchange = queue.Exchange };
                    await context.Storage.PersistentStorage.SaveQueueAsync(context.User.Username, queueEnity, context.CancellationToken);
                }
            }
            catch (ServerException)
            {
                throw;
            }
            catch
            {
                userQueues.TryRemove(queue.Name, out var _);
                queue.Dispose();
                throw;
            }

            return Confirmation.Ok(command);
        }

        public void Dispose()
        {
        }
    }
}
