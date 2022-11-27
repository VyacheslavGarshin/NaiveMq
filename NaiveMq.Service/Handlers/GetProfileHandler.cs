using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class GetProfileHandler : IHandler<GetProfile, GetProfileResponse>
    {
        public Task<GetProfileResponse> ExecuteAsync(ClientContext context, GetProfile command)
        {
            context.CheckUser(context);

            return Task.FromResult(new GetProfileResponse
            {
                Profile = new ProfileEntity
                {
                    Username = context.User.Username,
                    IsAdministrator = context.User.IsAdministrator
                }
            });
        }

        public void Dispose()
        {
        }
    }
}
