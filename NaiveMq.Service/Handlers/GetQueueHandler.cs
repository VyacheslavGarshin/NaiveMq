﻿using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class GetQueueHandler : IHandler<GetQueue, GetQueueResponse>
    {
        public Task<GetQueueResponse> ExecuteAsync(HandlerContext context, GetQueue command)
        {
            if (context.Storage.Queues.TryGetValue(command.Name, out var queue))
            {
                return Task.FromResult(new GetQueueResponse
                {
                    Queue = new QueueEntity
                    {
                        Name = queue.Name,
                        Durable = queue.Durable
                    }
                });
            }
            else
            {
                throw new ServerException(ErrorCode.QueueNotFound, string.Format(ErrorCode.QueueNotFound.GetDescription(), command.Name));
            }
        }

        public void Dispose()
        {
        }
    }
}
