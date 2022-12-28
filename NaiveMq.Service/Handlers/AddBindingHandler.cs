using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using System.Collections.Concurrent;
using NaiveMq.Service.Entities;
using NaiveMq.Service.Enums;

namespace NaiveMq.Service.Handlers
{
    public class AddBindingHandler : AbstractHandler<AddBinding, Confirmation>
    {
        public override async Task<Confirmation> ExecuteAsync(ClientContext context, AddBinding command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            var bindingEnity = BindingEntity.FromCommand(command);
            
            await ExecuteEntityAsync(context, bindingEnity, cancellationToken);

            return Confirmation.Ok(command);
        }

        public async Task ExecuteEntityAsync(ClientContext context, BindingEntity bindingEntity, CancellationToken cancellationToken)
        {
            var binding = new BindingCog(bindingEntity);

            Check(context, binding);

            Bind(context, binding, out var exchangeBindings, out var queueBindings);

            if (context.Mode == ClientContextMode.Client && bindingEntity.Durable)
            {
                try
                {
                    await context.Storage.PersistentStorage.SaveBindingAsync(context.User.Entity.Username, bindingEntity, cancellationToken);
                }
                catch
                {
                    if (exchangeBindings.IsEmpty)
                    {
                        context.User.Bindings.TryRemove(bindingEntity.Exchange, out var _);
                    }

                    if (queueBindings.IsEmpty)
                    {
                        context.User.Bindings.TryRemove(bindingEntity.Queue, out var _);
                    }

                    throw;
                }
            }
        }

        private static void Check(ClientContext context, BindingCog binding)
        {
            if (!context.User.Queues.TryGetValue(binding.Entity.Exchange, out var exchange))
            {
                throw new ServerException(ErrorCode.ExchangeNotFound, new[] { binding.Entity.Exchange });
            }

            if (!context.User.Queues.TryGetValue(binding.Entity.Queue, out var queue))
            {
                throw new ServerException(ErrorCode.QueueNotFound, new[] { binding.Entity.Queue });
            }

            if (queue.Entity.Exchange)
            {
                throw new ServerException(ErrorCode.BindExchange);
            }

            if (!exchange.Entity.Exchange)
            {
                throw new ServerException(ErrorCode.BindToQueue);
            }

            if (binding.Entity.Durable && (!exchange.Entity.Durable || !queue.Entity.Durable))
            {
                throw new ServerException(ErrorCode.DurableBindingCheck);
            }
        }

        private static void Bind(
            ClientContext context, 
            BindingCog binding,
            out ConcurrentDictionary<string, BindingCog> exchangeBindings,
            out ConcurrentDictionary<string, BindingCog> queueBindings)
        {            
            if (!context.User.Bindings.TryGetValue(binding.Entity.Exchange, out exchangeBindings))
            {
                exchangeBindings = new(StringComparer.InvariantCultureIgnoreCase);
                context.User.Bindings.TryAdd(binding.Entity.Exchange, exchangeBindings);
            }

            if (!exchangeBindings.TryAdd(binding.Entity.Queue, binding))
            {
                throw new ServerException(ErrorCode.BindingAlreadyExists, new[] { binding.Entity.Exchange, binding.Entity.Queue });
            }

            if (!context.User.Bindings.TryGetValue(binding.Entity.Queue, out queueBindings))
            {
                queueBindings = new(StringComparer.InvariantCultureIgnoreCase);
                context.User.Bindings.TryAdd(binding.Entity.Queue, queueBindings);
            }

            if (!queueBindings.TryAdd(binding.Entity.Exchange, binding))
            {
                exchangeBindings.TryRemove(binding.Entity.Queue, out var _);

                throw new ServerException(ErrorCode.BindingAlreadyExists, new[] { binding.Entity.Exchange, binding.Entity.Queue });
            }
        }

        public void Dispose()
        {
        }
    }
}
