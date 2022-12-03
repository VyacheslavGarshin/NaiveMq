using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Entities;

namespace NaiveMq.Service.Handlers
{
    public class SearchQueuesHandler : IHandler<SearchQueues, SearchQueuesResponse>
    {
        public Task<SearchQueuesResponse> ExecuteAsync(ClientContext context, SearchQueues command)
        {
            context.CheckUser(context);

            var userQueues = context.User.Administrator 
                ? context.Storage.UserQueues.SelectMany(x => x.Value.Values.Where(y => string.IsNullOrEmpty(command.User) || y.User.Contains(command.User, StringComparison.InvariantCultureIgnoreCase)))
                : context.Storage.GetUserQueues(context).Values;

            return Task.FromResult(SearchQueuesResponse.Ok(command, (response) =>
            {
                response.Queues = userQueues.Where(x => string.IsNullOrEmpty(command.Name) || x.Name.Contains(command.Name, StringComparison.InvariantCultureIgnoreCase)).Select(x =>
                    new QueueEntity
                    {
                        User = x.User,
                        Name = x.Name,
                        Durable = x.Durable,
                        Exchange = x.Exchange,
                    }).OrderBy(x => x.User).ThenBy(x => x.Name).ToList();
            }));
        }

        public void Dispose()
        {
        }
    }
}
