using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Handlers
{
    public class AddBindingHandler : IHandler<AddBinding, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, AddBinding command)
        {
            context.CheckUser(context);

            var userBindings = context.Storage.GetUserBindings(context);
            var userQueues = context.Storage.GetUserQueues(context);

            var binding = new Binding
            {
                Exchange = command.Exchange,
                Queue = command.Queue,
                Durable = command.Durable,
                Regex = string.IsNullOrEmpty(command.Regex) ? null : new Regex(command.Regex, RegexOptions.IgnoreCase),
            };

            Check(userQueues, binding);

            Bind(userBindings, binding, out var exchangeBindings, out var queueBindings);

            if (!context.Reinstate && command.Durable)
            {
                try
                {
                    var bindingEnity = new BindingEntity { Exchange = binding.Exchange, Queue = binding.Queue, Durable = binding.Durable, Regex = command.Regex };
                    await context.Storage.PersistentStorage.SaveBindingAsync(context.User.Username, bindingEnity, context.CancellationToken);
                }
                catch
                {
                    if (exchangeBindings.IsEmpty)
                    {
                        userBindings.TryRemove(command.Exchange, out var _);
                    }

                    if (queueBindings.IsEmpty)
                    {
                        userBindings.TryRemove(command.Queue, out var _);
                    }

                    throw;
                }
            }

            return Confirmation.Ok(command);
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
                throw new ServerException(ErrorCode.CannotBindExchange, ErrorCode.CannotBindExchange.GetDescription());
            }

            if (!exchange.Exchange)
            {
                throw new ServerException(ErrorCode.CannotBindToQueue, ErrorCode.CannotBindToQueue.GetDescription());
            }

            if (binding.Durable && (!exchange.Durable || !queue.Durable))
            {
                throw new ServerException(ErrorCode.DurableBindingCheck, ErrorCode.DurableBindingCheck.GetDescription());
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
