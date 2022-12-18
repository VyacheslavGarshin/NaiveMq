using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class GetServerHandler : IHandler<GetServer, GetServerResponse>
    {
        public Task<GetServerResponse> ExecuteAsync(ClientContext context, GetServer command)
        {
            return Task.FromResult(GetServerResponse.Ok(command, (response) =>
            {
                response.Entity = new Server
                {
                    Name = context.Storage.Options.Name,
                };
            }));
        }

        public void Dispose()
        {
        }
    }
}
