﻿using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class GetQueueHandler : AbstractHandler<GetQueue, GetQueueResponse>
    {
        public override Task<GetQueueResponse> ExecuteAsync(ClientContext context, GetQueue command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            if (!(context.User.Queues.TryGetValue(command.Name, out var queue) || command.Try))
            {
                throw new ServerException(ErrorCode.QueueNotFound, new[] { command.Name });
            }

            return Task.FromResult(GetQueueResponse.Ok(command, (response) =>
            {
                response.Entity = queue != null
                    ? new Queue
                    {
                        User = queue.Entity.User,
                        Name = queue.Entity.Name,
                        Durable = queue.Entity.Durable,
                        Exchange = queue.Entity.Exchange,
                        LengthLimit = queue.Entity.LengthLimit,
                        VolumeLimit = queue.Entity.VolumeLimit,
                        LimitStrategy = queue.Entity.LimitStrategy,
                        Status = queue.Status,
                    }
                    : null;
            }));
        }
    }
}
