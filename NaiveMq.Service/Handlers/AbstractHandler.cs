using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Handlers
{
    public abstract class AbstractHandler<TRequest, TResponse> : IHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : IResponse
    {
        public async Task<IResponse> ExecuteAsync(ClientContext context, IRequest command, CancellationToken cancellationToken)
        {
            return await ExecuteAsync(context, (TRequest)command, cancellationToken);
        }

        public abstract Task<TResponse> ExecuteAsync(ClientContext context, TRequest command, CancellationToken cancellationToken);
    }
}
