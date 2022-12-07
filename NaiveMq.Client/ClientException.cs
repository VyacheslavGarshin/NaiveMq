using NaiveMq.Client.Common;
using System;

namespace NaiveMq.Client
{
    public class ClientException : Exception
    {
        public ErrorCode ErrorCode { get; set; }

        public ClientException(ErrorCode errorCode) : this(errorCode, errorCode.GetDescription(), null)
        {
        }
        
        public ClientException(ErrorCode errorCode, string message) : this(errorCode, message, null)
        {
        }

        public ClientException(ErrorCode errorCode, Exception innerException) : this(errorCode, errorCode.GetDescription(), innerException)
        {
        }

        public ClientException(ErrorCode errorCode, string message, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}
