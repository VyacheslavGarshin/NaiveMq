using System;

namespace NaiveMq.Client.Exceptions
{
    public class ParseCommandException : Exception
    {
        public ErrorCode ErrorCode { get; set; }

        public ParseCommandException(ErrorCode errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
