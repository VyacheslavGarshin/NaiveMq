using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Handlers
{
    public class ResponseHandler : IHandler<Response, Confirmation>
    {
        public async Task<Confirmation> ExecuteAsync(ClientContext context, Response command)
        {
            context.CheckUser(context);

            var clientId = context.Storage.ClientRequests.RemoveRequest(command.RequestId);

            if (clientId != null)
            {
                if (context.Storage.TryGetClient(clientId.Value, out var receiverContext))
                {
                    var confirmation = new Confirmation
                    {
                        RequestId = command.RequestId,
                        Success = command.Success,
                        ErrorCode = command.ErrorCode,
                        ErrorMessage = command.ErrorMessage,
                        Text = command.Text
                    };

                    await receiverContext.Client.SendAsync(confirmation, context.CancellationToken);
                }
            }

            return Confirmation.Ok(command);
        }

        public void Dispose()
        {
        }
    }
}
