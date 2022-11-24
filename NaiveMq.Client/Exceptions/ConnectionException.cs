using System;

namespace NaiveMq.Client.Exceptions
{
    public class ConnectionException : Exception
    {
        public ConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
