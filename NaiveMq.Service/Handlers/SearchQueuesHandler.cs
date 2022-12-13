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

            var queues = (context.User.Entity.Administrator 
                ? context.Storage.Users
                    .Where(y => string.IsNullOrEmpty(command.User) || y.Value.Entity.Username.Contains(command.User, StringComparison.InvariantCultureIgnoreCase))
                    .SelectMany(x => x.Value.Queues)
                : context.User.Queues).Select(x => x.Value);

            var expression = queues
                .Where(x => string.IsNullOrEmpty(command.Name) || x.Entity.Name.Contains(command.Name, StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(x => x.Entity.User).ThenBy(x => x.Entity.Name);

            return Task.FromResult(SearchQueuesResponse.Ok(command, (response) =>
            {
                response.Entities = expression.Skip(command.Skip).Take(command.Take).Select(x =>
                    new Queue
                    {
                        User = x.Entity.User,
                        Name = x.Entity.Name,
                        Durable = x.Entity.Durable,
                        Exchange = x.Entity.Exchange,
                        Limit = x.Entity.Limit,
                        LimitBy = x.Entity.LimitBy,
                        LimitStrategy = x.Entity.LimitStrategy,
                    }).ToList();

                response.Count = command.Count ? expression.Count() : null;
            }));
        }

        public void Dispose()
        {
        }
    }
}
