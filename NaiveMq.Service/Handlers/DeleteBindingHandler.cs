using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Service.Enums;

namespace NaiveMq.Service.Handlers
{
    public class DeleteBindingHandler : AbstractHandler<DeleteBinding, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, DeleteBinding command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            if (!(context.User.Bindings.TryGetValue(command.Exchange, out var exchangeBindings)
                && exchangeBindings.TryRemove(command.Queue, out var binding)))
            {
                throw new ServerException(ErrorCode.BindingNotFound, new object[] { command.Exchange, command.Queue });
            }

            if (!(context.User.Bindings.TryGetValue(command.Queue, out var queueBindings)
                && queueBindings.TryRemove(command.Exchange, out var _)))
            {
                // todo not clear what to do
            }

            if (context.Mode == ClientContextMode.Client && binding.Entity.Durable)
            {
                try
                {
                    await context.Storage.PersistentStorage.DeleteBindingAsync(context.User.Entity.Username, binding.Entity.Exchange, binding.Entity.Queue, cancellationToken);
                }
                catch (Exception)
                {
                    exchangeBindings.TryAdd(binding.Entity.Queue, binding);
                    queueBindings.TryAdd(binding.Entity.Exchange, binding);
                    
                    throw;
                }
            }

            if (exchangeBindings.IsEmpty)
            {
                context.User.Bindings.TryRemove(command.Exchange, out var _);
            }

            if (queueBindings != null && queueBindings.IsEmpty)
            {
                context.User.Bindings.TryRemove(command.Queue, out var _);
            }

            return Confirmation.Ok(command);
        }
    }
}
