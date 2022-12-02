using System;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractResponse<T> : IResponse
        where T : IResponse, new()
    {
        public Guid Id { get; set; }

        public Guid RequestId { get; set; }

        public bool Success { get; set; } = true;

        public string ErrorCode { get; set; }

        public string ErrorMessage { get; set; }

        public static T Ok(Guid requestId)
        {
            return new T
            {
                RequestId = requestId,
                Success = true,
            };
        }

        public static T Ok(IRequest request)
        {
            return request.Confirm ? new T { RequestId = request.Id, Success = true } : default;
        }

        public static T Error(Guid requestId, string errorCode, string errorMessage)
        {
            return new T
            {
                RequestId = requestId,
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
            };
        }

        public static T Error(string errorCode, string errorMessage)
        {
            return Error(Guid.Empty, errorCode, errorMessage);
        }
    }
}
