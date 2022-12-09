using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;
using System.Text.RegularExpressions;
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

        public async Task ExecuteEntityAsync(ClientContext context, BindingEntity bindingEnity)
        {
            var userBindings = context.Storage.GetUserBindings(context);
            var userQueues = context.Storage.GetUserQueues(context);

            var binding = new Binding
            {
                Exchange = bindingEnity.Exchange,
                Queue = bindingEnity.Queue,
                Durable = bindingEnity.Durable,
                Pattern = string.IsNullOrEmpty(bindingEnity.Pattern) ? null : new Regex(bindingEnity.Pattern, RegexOptions.IgnoreCase),
            };

            Check(userQueues, binding);

            Bind(userBindings, binding, out var exchangeBindings, out var queueBindings);

            if (!context.Reinstate && bindingEnity.Durable)
            {
                try
                {
                    await context.Storage.PersistentStorage.SaveBindingAsync(context.User.Username, bindingEnity, context.CancellationToken);
                }
                catch
                {
                    if (exchangeBindings.IsEmpty)
                    {
                        userBindings.TryRemove(bindingEnity.Exchange, out var _);
                    }

                    if (queueBindings.IsEmpty)
                    {
                        userBindings.TryRemove(bindingEnity.Queue, out var _);
                    }

                    throw;
                }
            }
        }

        private static void Check(ConcurrentDictionary<string, Queue> userQueues, Binding binding)
        {
            if (!userQueues.TryGetValue(binding.Exchange, out var exchange))
            {
                throw new ServerException(ErrorCode.ExchangeNotFound, string.Format(ErrorCode.ExchangeNotFound.GetDescription(), binding.Exchange));
            }

            if (!userQueues.TryGetValue(binding.Queue, out var queue))
            {
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), binding.Queue));
            }

            if (queue.Exchange)
            {
                throw new ServerException(ErrorCode.CannotBindExchange);
            }

            if (!exchange.Exchange)
            {
                throw new ServerException(ErrorCode.CannotBindToQueue);
            }

            if (binding.Durable && (!exchange.Durable || !queue.Durable))
            {
                throw new ServerException(ErrorCode.DurableBindingCheck);
            }
        }

        private static void Bind(
            ConcurrentDictionary<string, ConcurrentDictionary<string, Binding>> userBindings, 
            Binding binding,
            out ConcurrentDictionary<string, Binding> exchangeBindings,
            out ConcurrentDictionary<string, Binding> queueBindings)
        {            
            if (!userBindings.TryGetValue(binding.Exchange, out exchangeBindings))
            {
                exchangeBindings = new(StringComparer.InvariantCultureIgnoreCase);
                userBindings.TryAdd(binding.Exchange, exchangeBindings);
            }

            if (!exchangeBindings.TryAdd(binding.Queue, binding))
            {
                throw new ServerException(ErrorCode.ExchangeAlreadyBoundToQueue, string.Format(ErrorCode.ExchangeAlreadyBoundToQueue.GetDescription(), binding.Exchange, binding.Queue));
            }

            if (!userBindings.TryGetValue(binding.Queue, out queueBindings))
            {
                queueBindings = new(StringComparer.InvariantCultureIgnoreCase);
                userBindings.TryAdd(binding.Queue, queueBindings);
            }

            if (!queueBindings.TryAdd(binding.Exchange, binding))
            {
                exchangeBindings.TryRemove(binding.Queue, out var _);

                throw new ServerException(ErrorCode.ExchangeAlreadyBoundToQueue, string.Format(ErrorCode.ExchangeAlreadyBoundToQueue.GetDescription(), binding.Exchange, binding.Queue));
            }
        }

        public void Dispose()
        {
        }
    }
}
