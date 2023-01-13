using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class GetBindingHandler : AbstractHandler<GetBinding, GetBindingResponse>
    {
        public override Task<GetBindingResponse> ExecuteAsync(ClientContext context, GetBinding command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            BindingCog binding = null;

            if (!(context.User.Bindings.TryGetValue(command.Exchange, out var exchangeBindings)
                && exchangeBindings.TryGetValue(command.Queue, out binding) || command.Try))
            {
                throw new ServerException(ErrorCode.BindingNotFound, new[] { command.Exchange, command.Queue });
            }

            return Task.FromResult(GetQueueResponse.Ok(command, (response) =>
            {
                response.Entity = binding != null
                    ? new Binding
                    {
                        Exchange = binding.Entity.Exchange,
                        Queue = binding.Entity.Queue,
                        Durable = binding.Entity.Durable,
                        Pattern = binding.Entity.Pattern,
                    }
                    : null;
            }));
        }
    }
}
