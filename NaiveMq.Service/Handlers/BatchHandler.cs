using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Handlers
{
    public class BatchHandler : AbstractHandler<Batch, BatchResponse>
    {
        public override async Task<BatchResponse> ExecuteAsync(ClientContext context, Batch command)
        {
            context.CheckUser(context);

            var responses = new List<IResponse>();

            foreach (var request in command.Requests)
            {
                try
                {
                    responses.Add(await context.Storage.Service.ExecuteCommandAsync(request, context));
                }
                catch (Exception ex)
                {
                    responses.Add(Confirmation.Error(
                        command,
                        ex is ServerException serverException ? serverException.ErrorCode.ToString() : string.Empty,
                        ex.GetBaseException().Message));
                }
            }

            return BatchResponse.Ok(command, (response) =>
            {
                response.Responses = responses;
            });
        }
    }
}
