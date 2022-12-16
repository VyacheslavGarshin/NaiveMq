using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class ClearQueueHandler : IHandler<ClearQueue, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, ClearQueue command)
        {
            context.CheckUser(context);

            QueueCog queue = null;

            try
            {
                if (context.User.Queues.TryGetValue(command.Name, out queue))
                {
                    queue.Started = false;

                    queue.Clear();

                    if (queue.Entity.Durable)
                    {
                        await context.Storage.PersistentStorage.DeleteMessagesAsync(queue.Entity.User, queue.Entity.Name, context.StoppingToken);
                    }
                }
                else
                {
                    throw new ServerException(ErrorCode.QueueNotFound, new object[] { command.Name });
                }
            }
            finally
            {
                if (queue != null)
                {
                    queue.Started = true;
                }
            }

            return Confirmation.Ok(command);
        }

        public void Dispose()
        {
        }
    }
}
