using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Handlers
{
    public class BatchHandler : IHandler<Batch, BatchResponse>
    {
        public async Task<BatchResponse> ExecuteAsync(ClientContext context, Batch command)
        {
            context.CheckUser(context);

            var confirmations = new List<Confirmation>();

            foreach (var message in command.Messages)
            {
                try
                {
                    using var handler = new MessageHandler();
                    confirmations.Add(await handler.ExecuteAsync(context, message));

                    // main server handler doesn't know contents of commands and cannot count batch messages
                    context.Storage.ReadMessageCounter.Add();
                }
                catch (Exception ex)
                {
                    confirmations.Add(Confirmation.Error(
                        command.Id,
                        ex is ServerException serverException ? serverException.ErrorCode.ToString() : string.Empty,
                        ex.GetBaseException().Message));
                }
            }

            return BatchResponse.Ok(command, (response) =>
            {
                response.Confirmations = confirmations;
            });
        }

        public void Dispose()
        {
        }
    }
}
