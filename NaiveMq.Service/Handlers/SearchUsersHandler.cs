using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class SearchUsersHandler : IHandler<SearchUsers, SearchUsersResponse>
    {
        public Task<SearchUsersResponse> ExecuteAsync(ClientContext context, SearchUsers command)
        {
            context.CheckAdmin(context);

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

        public void Dispose()
        {
        }
    }
}
