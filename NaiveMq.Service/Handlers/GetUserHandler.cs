using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class GetUserHandler : IHandler<GetUser, GetUserResponse>
    {
        public Task<GetUserResponse> ExecuteAsync(ClientContext context, GetUser command)
        {
            context.CheckAdmin(context);
            
            if (context.Storage.Users.TryGetValue(command.Username, out var userEntity) || command.Try)
            {
                return Task.FromResult(new GetUserResponse
                {
                    User = userEntity != null
                        ? new UserEntity
                        {
                            Username = userEntity.Username,
                            Administrator = userEntity.Administrator
                        }
                        : null
                });
            }
            else
            {
                throw new ServerException(ErrorCode.UserNotFound, string.Format(ErrorCode.UserNotFound.GetDescription(), command.Username));
            }
        }

        public void Dispose()
        {
        }
    }
}
