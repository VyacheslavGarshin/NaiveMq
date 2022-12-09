using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class SearchQueuesHandler : IHandler<SearchQueues, SearchQueuesResponse>
    {
        public Task<SearchQueuesResponse> ExecuteAsync(ClientContext context, SearchQueues command)
        {
            context.CheckUser(context);

            var userQueues = context.User.Administrator 
                ? context.Storage.UserQueues.SelectMany(x => x.Value.Values.Where(y => string.IsNullOrEmpty(command.User) || y.Entity.User.Contains(command.User, StringComparison.InvariantCultureIgnoreCase)))
                : context.Storage.GetUserQueues(context).Values;

            return Task.FromResult(SearchQueuesResponse.Ok(command, (response) =>
            {
                response.Queues = userQueues.Where(x => string.IsNullOrEmpty(command.Name) || x.Entity.Name.Contains(command.Name, StringComparison.InvariantCultureIgnoreCase)).Select(x =>
                    new Queue
                    {
                        User = x.Entity.User,
                        Name = x.Entity.Name,
                        Durable = x.Entity.Durable,
                        Exchange = x.Entity.Exchange,
                        Limit = x.Entity.Limit,
                        LimitBy = x.Entity.LimitBy,
                        LimitStrategy = x.Entity.LimitStrategy,
                    }).OrderBy(x => x.User).ThenBy(x => x.Name).ToList();
            }));
        }

        public void Dispose()
        {
        }
    }
}
