using System;

namespace NaiveMq.Client.Exceptions
{
    public class ClientStoppedException : Exception
    {
        public ClientStoppedException(string message) : base(message)
        {
        }
    }
}
