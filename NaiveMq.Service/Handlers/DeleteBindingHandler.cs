using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class DeleteBindingHandler : IHandler<DeleteBinding, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, DeleteBinding command)
        {
            context.CheckUser(context);

            var userBindings = context.Storage.GetUserBindings(context);

            if (!(userBindings.TryGetValue(command.Exchange, out var exchangeBindings)
                && exchangeBindings.TryRemove(command.Queue, out var binding)))
            {
                throw new ServerException(ErrorCode.BindingNotFound, string.Format(ErrorCode.BindingNotFound.GetDescription(), command.Exchange, command.Queue));
            }

            if (!(userBindings.TryGetValue(command.Queue, out var queueBindings)
                && queueBindings.TryRemove(command.Exchange, out var _)))
            {
                // todo not clear what to do
            }

            if (binding.Entity.Durable)
            {
                try
                {
                    await context.Storage.PersistentStorage.DeleteBindingAsync(context.User.Username, binding.Entity.Exchange, binding.Entity.Queue, context.CancellationToken);
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
                userBindings.TryRemove(command.Exchange, out var _);
            }

            if (queueBindings != null && queueBindings.IsEmpty)
            {
                userBindings.TryRemove(command.Queue, out var _);
            }

            return Confirmation.Ok(command);
        }

        public void Dispose()
        {
        }
    }
}
