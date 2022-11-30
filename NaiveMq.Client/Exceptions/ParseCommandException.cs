namespace NaiveMq.Client.Exceptions
{
    public class ParseCommandException : ClientException
    {
        public ErrorCode ErrorCode { get; set; }

        public ParseCommandException(ErrorCode errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
