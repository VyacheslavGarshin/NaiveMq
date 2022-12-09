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

            var binding = new BindingCog(bindingEnity)
            {
                Pattern = string.IsNullOrEmpty(bindingEnity.Pattern) ? null : new Regex(bindingEnity.Pattern, RegexOptions.IgnoreCase),
            };

            Check(userQueues, binding);

            Bind(userBindings, binding, out var exchangeBindings, out var queueBindings);

            if (!context.Reinstate && bindingEnity.Durable)
            {
                try
                {
                    await context.Storage.PersistentStorage.SaveBindingAsync(context.User.Username, bindingEnity, context.StoppingToken);
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

        private static void Check(ConcurrentDictionary<string, QueueCog> userQueues, BindingCog binding)
        {
            if (!userQueues.TryGetValue(binding.Entity.Exchange, out var exchange))
            {
                throw new ServerException(ErrorCode.ExchangeNotFound, string.Format(ErrorCode.ExchangeNotFound.GetDescription(), binding.Entity.Exchange));
            }

            if (!userQueues.TryGetValue(binding.Entity.Queue, out var queue))
            {
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), binding.Entity.Queue));
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
            ConcurrentDictionary<string, ConcurrentDictionary<string, BindingCog>> userBindings, 
            BindingCog binding,
            out ConcurrentDictionary<string, BindingCog> exchangeBindings,
            out ConcurrentDictionary<string, BindingCog> queueBindings)
        {            
            if (!userBindings.TryGetValue(binding.Entity.Exchange, out exchangeBindings))
            {
                exchangeBindings = new(StringComparer.InvariantCultureIgnoreCase);
                userBindings.TryAdd(binding.Entity.Exchange, exchangeBindings);
            }

            if (!exchangeBindings.TryAdd(binding.Entity.Queue, binding))
            {
                throw new ServerException(ErrorCode.ExchangeAlreadyBoundToQueue, string.Format(ErrorCode.ExchangeAlreadyBoundToQueue.GetDescription(), binding.Entity.Exchange, binding.Entity.Queue));
            }

            if (!userBindings.TryGetValue(binding.Entity.Queue, out queueBindings))
            {
                queueBindings = new(StringComparer.InvariantCultureIgnoreCase);
                userBindings.TryAdd(binding.Entity.Queue, queueBindings);
            }

            if (!queueBindings.TryAdd(binding.Entity.Exchange, binding))
            {
                exchangeBindings.TryRemove(binding.Entity.Queue, out var _);

                throw new ServerException(ErrorCode.ExchangeAlreadyBoundToQueue, string.Format(ErrorCode.ExchangeAlreadyBoundToQueue.GetDescription(), binding.Entity.Exchange, binding.Entity.Queue));
            }
        }

        public void Dispose()
        {
        }
    }
}
