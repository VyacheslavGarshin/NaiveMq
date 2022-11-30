namespace NaiveMq.Client.Exceptions
{
    public class ClientStoppedException : ClientException
    {
        public ClientStoppedException(string message) : base(message)
        {
        }
    }
}
