using System;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractResponse<T> : AbstractCommand, IResponse
        where T : IResponse
    {
        public Guid RequestId { get; set; }

        public string RequestTag { get; set; }

        public bool Success { get; set; } = true;

        public string ErrorType { get; set; }

        public string ErrorCode { get; set; }

        public string ErrorMessage { get; set; }

        /// <summary>
        /// Returns new(T) with set <see cref="RequestId"/> if <see cref="request.Confirm"/>.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static TResult Ok<TResult>(IRequest<TResult> request, Action<TResult> action = null)
            where TResult : IResponse, new()
        {
            var result = (request?.Confirm ?? false) 
                ? new TResult { RequestId = request.Id, RequestTag = request.Tag, Success = true } 
                : default;

            if (result != null)
            {
                action?.Invoke(result);
            }

            return result;
        }

        public static T Error(IRequest request, string errorType, string errorCode, string errorMessage)
        {
            var result = Activator.CreateInstance<T>();

            result.RequestId = request.Id;
            result.RequestTag = request.Tag;
            result.Success = false;
            result.ErrorType = errorType;
            result.ErrorCode = errorCode;
            result.ErrorMessage = errorMessage;

            return result;
        }

        public virtual T Copy()
        {
            var result = Activator.CreateInstance<T>();

            result.RequestId = RequestId;
            result.RequestTag = RequestTag;
            result.Success = Success;
            result.ErrorType = ErrorType;
            result.ErrorCode = ErrorCode;
            result.ErrorMessage = ErrorMessage;

            return result;
        }

        public override void Validate()
        {
            if (RequestId == Guid.Empty)
            {
                throw new ClientException(Client.ErrorCode.RequestIdNotSet);
            }
        }
    }
}
