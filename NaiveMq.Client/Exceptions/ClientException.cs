using System;

namespace NaiveMq.Client.Exceptions
{
    public class ClientException : Exception
    {
        protected ClientException()
        {
        }

        protected ClientException(string message) : base(message)
        {
        }

        protected ClientException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
