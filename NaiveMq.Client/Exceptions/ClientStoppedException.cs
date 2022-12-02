using System;

namespace NaiveMq.Client.Exceptions
{
    public class ClientStoppedException : ClientException
    {
        public ClientStoppedException(string message) : base(message)
        {
        }

        public ClientStoppedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
