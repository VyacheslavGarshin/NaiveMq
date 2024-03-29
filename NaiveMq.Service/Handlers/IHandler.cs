﻿using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Handlers
{
    public interface IHandler
    {
        Task<IResponse> ExecuteAsync(ClientContext context, IRequest command, CancellationToken cancellationToken);
    }

    public interface IHandler<TRequest, TResponse> : IHandler
        where TRequest : IRequest<TResponse>
        where TResponse : IResponse
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="command"></param>
        /// <returns>If returns null and confirmation requested then Confirmation.Success will be returned. ResponseId will be set.</returns>
        Task<TResponse> ExecuteAsync(ClientContext context, TRequest command, CancellationToken cancellationToken);
    }
}