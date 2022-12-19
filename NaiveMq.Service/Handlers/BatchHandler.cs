using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Handlers
{
    public class BatchHandler : IHandler<Batch, BatchResponse>
    {
        public async Task<BatchResponse> ExecuteAsync(ClientContext context, Batch command)
        {
            context.CheckUser(context);

            var responses = new List<IResponse>();

            foreach (var request in command.Requests)
            {
                try
                {
                    var response = await context.Storage.Service.ExecuteCommandAsync(request, context);
                    responses.Add(response);
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

        public void Dispose()
        {
        }
    }
}
