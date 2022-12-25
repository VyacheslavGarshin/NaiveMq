using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class SearchUsersHandler : AbstractHandler<SearchUsers, SearchUsersResponse>
    {
        public override Task<SearchUsersResponse> ExecuteAsync(ClientContext context, SearchUsers command, CancellationToken cancellationToken)
        {
            context.CheckAdmin();

            var expression = context.Storage.Users.Values
                .Where(x => string.IsNullOrEmpty(command.Username) || x.Entity.Username.Contains(command.Username))
                .OrderBy(x => x.Entity.Username);

            return Task.FromResult(SearchUsersResponse.Ok(command, (response) =>
            {
                response.Entities = expression.Skip(command.Skip).Take(command.Take).Select(x =>
                    new User
                    {
                        Username = x.Entity.Username,
                        Administrator = x.Entity.Administrator
                    }).ToList();

                response.Count = command.Count ? expression.Count() : null;
            }));
        }
    }
}
