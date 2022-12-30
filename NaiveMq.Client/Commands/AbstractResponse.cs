using System;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractResponse<T> : AbstractCommand, IResponse
        where T : IResponse
    {
        [DataMember(Name = "RI")]
        public Guid RequestId { get; set; }

        [DataMember(Name = "RT")]
        public string RequestTag { get; set; }

        [DataMember(Name = "S")]
        public bool Success { get; set; } = true;

        [DataMember(Name = "EC")]
        public string ErrorCode { get; set; }

        [DataMember(Name = "EM")]
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

        public static T Error(IRequest request, string errorCode, string errorMessage)
        {
            var result = Activator.CreateInstance<T>();

            result.RequestId = request.Id;
            result.RequestTag = request.Tag;
            result.Success = false;
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
