using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Handlers
{
    public interface IHandler
    {
    }

    public interface IHandler<TRequest, TResponse> : IDisposable, IHandler
        where TRequest : IRequest<TResponse>
        where TResponse : IResponse
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="command"></param>
        /// <returns>If returns null and confirmation requested then Confirmation.Success will be returned. ResponseId will be set.</returns>
        Task<TResponse> ExecuteAsync(ClientContext context, TRequest command);
    }
}