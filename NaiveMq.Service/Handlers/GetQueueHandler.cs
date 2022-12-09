using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class GetQueueHandler : IHandler<GetQueue, GetQueueResponse>
    {
        public Task<GetQueueResponse> ExecuteAsync(ClientContext context, GetQueue command)
        {
            context.CheckUser(context);

            var userQueues = context.Storage.GetUserQueues(context);

            if (userQueues.TryGetValue(command.Name, out var queue) || command.Try)
            {
                return Task.FromResult(GetQueueResponse.Ok(command, (response) =>
                {
                    response.Queue = queue != null
                        ? new Queue
                        {
                            User = queue.Entity.User,
                            Name = queue.Entity.Name,
                            Durable = queue.Entity.Durable,
                            Exchange = queue.Entity.Exchange,
                            Limit = queue.Entity.Limit,
                            LimitType = queue.Entity.LimitType,
                            LimitStrategy = queue.Entity.LimitStrategy,
                        }
                        : null;
                }));
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
