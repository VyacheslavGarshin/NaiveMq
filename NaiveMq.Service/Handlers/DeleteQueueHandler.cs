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

            await DeleteBindings(context, command);

            if (context.User.Queues.TryRemove(command.Name, out var queue))
            {
                try
                {
                    if (queue.Entity.Durable)
                    {
                        await context.Storage.PersistentStorage.DeleteQueueAsync(queue.Entity.User, queue.Entity.Name, context.StoppingToken);
                    }

                    queue.Dispose();
                }
                catch
                {
                    context.User.Queues.TryAdd(command.Name, queue);
                    throw;
                }
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), command.Name));
            }

            return Confirmation.Ok(command);
        }

        private static async Task DeleteBindings(ClientContext context, DeleteQueue command)
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

        public void Dispose()
        {
        }
    }
}
