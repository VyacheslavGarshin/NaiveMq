using System;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractResponse<T> : IResponse
        where T : IResponse
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
            var result = Activator.CreateInstance<T>();

            result.RequestId = requestId;
            result.Success = true;

            return result;
        }

        /// <summary>
        /// Returns new(T) with set <see cref="RequestId"/> if <see cref="request.Confirm"/>.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static TResult Ok<TResult>(IRequest<TResult> request, Action<TResult> action = null)
            where TResult : IResponse, new()
        {
            var result = (request?.Confirm ?? false) ? new TResult { RequestId = request.Id, Success = true } : default;

            if (result != null)
            {
                action?.Invoke(result);
            }

            return result;
        }

        public static T Error(Guid requestId, string errorCode, string errorMessage)
        {
            var result = Activator.CreateInstance<T>();

            result.RequestId = requestId;
            result.Success = false;
            result.ErrorCode = errorCode;
            result.ErrorMessage = errorMessage;

            return result;
        }

        public virtual void Validate()
        {
            if (RequestId == Guid.Empty)
            {
                throw new ClientException(Client.ErrorCode.RequestIdNotSet);
            }
        }
    }
}
