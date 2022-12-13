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

            return Task.FromResult(SearchUsersResponse.Ok(command, (response) =>
            {
                response.Entities = context.Storage.Users.Values.Where(x => string.IsNullOrEmpty(command.Username) || x.Entity.Username.Contains(command.Username)).Select(x =>
                    new User
                    {
                        Username = x.Entity.Username,
                        Administrator = x.Entity.Administrator
                    }).OrderBy(x => x.Username).ToList();
            }));
        }

        public void Dispose()
        {
        }
    }
}
