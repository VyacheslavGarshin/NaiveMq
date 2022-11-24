using NaiveMq.Client;

namespace NaiveMq.Service
{
    public class ServerException : Exception
    {
        public ErrorCode ErrorCode { get; set; }

        public ServerException(ErrorCode errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
