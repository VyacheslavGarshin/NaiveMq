using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using System.Collections.Concurrent;
using NaiveMq.Service.Entities;

namespace NaiveMq.Service.Handlers
{
    public class AddBindingHandler : IHandler<AddBinding, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, AddBinding command)
        {
            context.CheckUser(context);

            var bindingEnity = new BindingEntity
            {
                Exchange = command.Exchange,
                Queue = command.Queue,
                Durable = command.Durable,
                Pattern = command.Pattern
            };
            
            await ExecuteEntityAsync(context, bindingEnity);

            return Confirmation.Ok(command);
        }

        public async Task ExecuteEntityAsync(ClientContext context, BindingEntity bindingEntity)
        {
            var binding = new BindingCog(bindingEntity);

            Check(context, binding);

            Bind(context, binding, out var exchangeBindings, out var queueBindings);

            if (!context.Reinstate && bindingEntity.Durable)
            {
                try
                {
                    await context.Storage.PersistentStorage.SaveBindingAsync(context.User.Entity.Username, bindingEntity, context.StoppingToken);
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
                throw new ServerException(ErrorCode.ExchangeNotFound, new object[] { binding.Entity.Exchange });
            }

            if (!context.User.Queues.TryGetValue(binding.Entity.Queue, out var queue))
            {
                throw new ServerException(ErrorCode.QueueNotFound, new object[] { binding.Entity.Queue });
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
                throw new ServerException(ErrorCode.ExchangeAlreadyBoundToQueue, new object[] { binding.Entity.Exchange, binding.Entity.Queue });
            }

            if (!context.User.Bindings.TryGetValue(binding.Entity.Queue, out queueBindings))
            {
                queueBindings = new(StringComparer.InvariantCultureIgnoreCase);
                context.User.Bindings.TryAdd(binding.Entity.Queue, queueBindings);
            }

            if (!queueBindings.TryAdd(binding.Entity.Exchange, binding))
            {
                exchangeBindings.TryRemove(binding.Entity.Queue, out var _);

                throw new ServerException(ErrorCode.ExchangeAlreadyBoundToQueue, new object[] { binding.Entity.Exchange, binding.Entity.Queue });
            }
        }

        public void Dispose()
        {
        }
    }
}
