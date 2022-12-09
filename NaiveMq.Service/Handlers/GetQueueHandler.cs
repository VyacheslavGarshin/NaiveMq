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
                        ? new QueueDto
                        {
                            User = queue.User,
                            Name = queue.Name,
                            Durable = queue.Durable,
                            Exchange = queue.Exchange,
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
