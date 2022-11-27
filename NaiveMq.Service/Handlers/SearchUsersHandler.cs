using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class SearchUsersHandler : IHandler<SearchUsers, SearchUsersResponse>
    {
        public Task<SearchUsersResponse> ExecuteAsync(ClientContext context, SearchUsers command)
        {
            context.CheckAdmin(context);

            return Task.FromResult(new SearchUsersResponse
            {
                Users = context.Storage.Users.Values.Where(x => string.IsNullOrEmpty(command.Username) || x.Username.Contains(command.Username)).Select(x =>
                    new UserEntity
                    {
                        Username = x.Username,
                        IsAdministrator = x.IsAdministrator
                    }).OrderBy(x => x.Username).ToList()
            });
        }

        public void Dispose()
        {
        }
    }
}
