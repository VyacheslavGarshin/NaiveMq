using NaiveMq.Service.Cogs;
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
            context.CheckUser(context);

            var userQueues = context.Storage.GetUserQueues(context);

            if (userQueues.TryGetValue(command.Name, out var queue) || command.Try)
            {
                return Task.FromResult(new GetQueueResponse
                {
                    Queue = queue != null
                        ? new QueueEntity
                        {
                            User = queue.User,
                            Name = queue.Name,
                            Durable = queue.Durable
                        }
                        : null
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
