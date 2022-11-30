using NaiveMq.Client.Commands;

namespace NaiveMq.Client.Exceptions
{
    public class ConfirmationException : ClientException
    {
        public IResponse Response { get; set; }

        public ConfirmationException(IResponse response) : base(response.ErrorMessage)
        {
            Response = response;
        }
    }
}
