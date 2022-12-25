using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class SearchQueuesHandler : AbstractHandler<SearchQueues, SearchQueuesResponse>
    {
        public override Task<SearchQueuesResponse> ExecuteAsync(ClientContext context, SearchQueues command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            if (!string.IsNullOrEmpty(command.User))
            {
                context.CheckAdmin();
            }

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
                        LengthLimit = x.Entity.LengthLimit,
                        VolumeLimit = x.Entity.VolumeLimit,
                        LimitStrategy = x.Entity.LimitStrategy,
                        Status = x.Status,
                    }).ToList();

                response.Count = command.Count ? expression.Count() : null;
            }));
        }
    }
}
