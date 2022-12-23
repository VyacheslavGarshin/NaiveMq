using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;

namespace NaiveMq.Service.Handlers
{
    public class GetServerHandler : AbstractHandler<GetServer, GetServerResponse>
    {
        public override Task<GetServerResponse> ExecuteAsync(ClientContext context, GetServer command, CancellationToken cancellationToken)
        {
            return Task.FromResult(GetServerResponse.Ok(command, (response) =>
            {
                response.Entity = new Server
                {
                    Name = context.Storage.Service.Options.Name,
                    ClusterKey = context.Storage.Service.Options.ClusterKey,
                };
            }));
        }
    }
}
