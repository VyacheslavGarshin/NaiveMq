using NaiveMq.Service.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client;

namespace NaiveMq.Service.Handlers
{
    public class TrackHandler : AbstractHandler<Track, TrackResponse>
    {
        public override Task<TrackResponse> ExecuteAsync(ClientContext context, Track command, CancellationToken cancellationToken)
        {
            context.CheckUser();

            TrackResponse result = null;

            if (command.Finish)
            {
                if (!context.Tracking)
                {
                    throw new ServerException(ErrorCode.TrackingNotStarted);
                }

                result = TrackResponse.Ok(command, (response) =>
                    {
                        response.FailedRequests = context.TrackFailedRequests.ToList();
                        response.LastErrorCode = context.TrackLastErrorCode;
                        response.LastErrorMessage = context.TrackLastErrorMessage;
                        response.Overflow = context.TrackOverflow;
                    });

                context.Tracking = false;
                context.TrackFailedRequests = new();
                context.TrackLastErrorCode = null;
                context.TrackLastErrorMessage = null;
                context.TrackOverflow = false;
            }

            if (command.Start)
            {
                if (context.Tracking)
                {
                    throw new ServerException(ErrorCode.TrackingAlreadyStarted);
                }

                context.Tracking = true;

                result ??= TrackResponse.Ok(command);
            }

            return Task.FromResult(result);
        }
    }
}
