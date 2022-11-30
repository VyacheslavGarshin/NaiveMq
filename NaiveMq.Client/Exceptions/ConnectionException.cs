using System;

namespace NaiveMq.Client.Exceptions
{
    public class ConnectionException : ClientException
    {
        public ConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
