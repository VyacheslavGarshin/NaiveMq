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

        /// <summary>
        /// Returns new(T) with set <see cref="RequestId"/>.
        /// </summary>
        /// <param name="requestId"></param>
        /// <returns></returns>
        public static T Ok(Guid requestId)
        {
            return new T
            {
                RequestId = requestId,
                Success = true,
            };
        }

        /// <summary>
        /// Returns new(T) with set <see cref="RequestId"/> if <see cref="request.Confirm"/>.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static T Ok(IRequest request, Action<T> action = null)
        {
            var result = (request?.Confirm ?? false) ? new T { RequestId = request.Id, Success = true } : default;

            action?.Invoke(result);

            return result;
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

        public virtual void Validate()
        {
        }
    }
}
