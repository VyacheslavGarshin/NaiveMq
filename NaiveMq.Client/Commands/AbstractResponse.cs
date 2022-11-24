using System;
using System.Collections.Generic;
using System.Linq;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractResponse<T> : IResponse
        where T : IResponse, new()
    {
        public Guid? Id { get; set; }

        public Guid? RequestId { get; set; }

        public bool IsSuccess { get; set; } = true;

        public string ErrorCode { get; set; }

        public string ErrorMessage { get; set; }

        public List<string> Warnings { get; set; }

        public static T Success(Guid? requestId, IEnumerable<string> warnings = null)
        {
            return new T
            {
                RequestId = requestId,
                IsSuccess = true,
                Warnings = warnings?.ToList()
            };
        }

        public static T Success(IEnumerable<string> warnings = null)
        {
            return Success(null, warnings);
        }

        public static T Error(Guid? requestId, string errorCode, string errorMessage, IEnumerable<string> warnings = null)
        {
            return new T
            {
                RequestId = requestId,
                IsSuccess = false,
                ErrorCode = errorCode.ToString(),
                ErrorMessage = errorMessage,
                Warnings = warnings?.ToList()
            };
        }

        public static T Error(string errorCode, string errorMessage, IEnumerable<string> warnings = null)
        {
            return Error(null, errorCode, errorMessage, warnings);
        }
    }
}
