using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class GetUserHandler : AbstractHandler<GetUser, GetUserResponse>
    {
        public override Task<GetUserResponse> ExecuteAsync(ClientContext context, GetUser command, CancellationToken cancellationToken)
        {
            context.CheckAdmin();
            
            if (context.Storage.Users.TryGetValue(command.Username, out var user) || command.Try)
            {
                return Task.FromResult(GetUserResponse.Ok(command, (response) =>
                {
                    response.Entity = user != null
                        ? new User
                        {
                            Username = user.Entity.Username,
                            Administrator = user.Entity.Administrator
                        }
                        : null;
                }));
            }
            else
            {
                throw new ServerException(ErrorCode.UserNotFound, new[] { command.Username });
            }
        }
    }
}
