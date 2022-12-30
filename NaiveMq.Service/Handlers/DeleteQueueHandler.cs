using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Service.Enums;
using NaiveMq.Client.Enums;

namespace NaiveMq.Service.Handlers
{
    public class DeleteQueueHandler : AbstractHandler<DeleteQueue, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, DeleteQueue command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            if (!context.User.Queues.TryGetValue(command.Name, out var queue))
            {
                throw new ServerException(ErrorCode.QueueNotFound, new[] { command.Name });
            }

            queue.SetStatus(QueueStatus.Deleting);

            await DeleteBindingsAsync(context, command, cancellationToken);

            if (queue.Entity.Durable)
            {
                switch (context.Mode)
                {
                    case ClientContextMode.Client:
                        await context.Storage.PersistentStorage.DeleteQueueAsync(queue.Entity.User, queue.Entity.Name, cancellationToken);
                        break;
                    case ClientContextMode.Replicate:
                        await context.Storage.PersistentStorage.DeleteMessagesAsync(queue.Entity.User, queue.Entity.Name, cancellationToken);
                        break;
                    case ClientContextMode.Reinstate:
                        break;
                }
            }

            queue.Dispose();
            queue.SetStatus(QueueStatus.Deleted);
            context.User.Queues.TryRemove(command.Name, out var _);

            return Confirmation.Ok(command);
        }

        private static async Task DeleteBindingsAsync(ClientContext context, DeleteQueue command, CancellationToken cancellationToken)
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

                    await new DeleteBindingHandler().ExecuteAsync(context, deleteBindingCommand, cancellationToken);
                }
            }
        }
    }
}
