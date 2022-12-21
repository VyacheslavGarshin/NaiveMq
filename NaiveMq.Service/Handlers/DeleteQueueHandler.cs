using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Service.Enums;

namespace NaiveMq.Service.Handlers
{
    public class DeleteQueueHandler : AbstractHandler<DeleteQueue, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, DeleteQueue command)
        {
            context.CheckUser(context);

            if (context.User.Queues.TryRemove(command.Name, out var queue))
            {
                queue.Status = QueueStatus.Deleting;

                try
                {
                    await DeleteBindingsAsync(context, command);

                    if (!context.Reinstate && queue.Entity.Durable)
                    {
                        await context.Storage.PersistentStorage.DeleteQueueAsync(queue.Entity.User, queue.Entity.Name, context.StoppingToken);
                    }

                    queue.Dispose();

                    queue.Status = QueueStatus.Deleted;
                }
                catch
                {
                    context.User.Queues.TryAdd(command.Name, queue);
                    throw;
                }
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, new object[] { command.Name });
            }

            return Confirmation.Ok(command);
        }

        private static async Task DeleteBindingsAsync(ClientContext context, DeleteQueue command)
        {
            if (context.User.Bindings.TryGetValue(command.Name, out var bindings))
            {
                foreach (var binding in bindings.Values.ToList())
                {
                    var deleteBindingCommand = new DeleteBinding
                    {
                        Exchange = binding.Entity.Exchange,
                        Queue = binding.Entity.Queue
                    };

                    await new DeleteBindingHandler().ExecuteAsync(context, deleteBindingCommand);
                }
            }
        }
    }
}
