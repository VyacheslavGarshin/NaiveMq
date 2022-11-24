using NaiveMq.Client.Commands;
using System;

namespace NaiveMq.Client.Exceptions
{
    public class ConfirmationException : Exception
    {
        public IResponse Response { get; set; }

        public ConfirmationException(IResponse response) : base(response.ErrorMessage)
        {
            Response = response;
        }
    }
}
