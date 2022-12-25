using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class GetProfileHandler : AbstractHandler<GetProfile, GetProfileResponse>
    {
        public override Task<GetProfileResponse> ExecuteAsync(ClientContext context, GetProfile command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            return Task.FromResult(GetProfileResponse.Ok(command, (response) =>
            {
                response.Entity = new Profile
                {
                    Username = context.User.Entity.Username,
                    Administrator = context.User.Entity.Administrator
                };
            }));
        }
    }
}
