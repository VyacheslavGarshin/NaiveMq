namespace NaiveMq.Client.Commands
{
    public interface IRequest : ICommand
    {
        public bool Confirm { get; set; }
    }

    public interface IRequest<TResponse> : IRequest
        where TResponse : IResponse
    {
    }
}
