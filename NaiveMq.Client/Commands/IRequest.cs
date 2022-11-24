namespace NaiveMq.Client.Commands
{
    public interface IRequest : ICommand
    {
        /// <summary>
        /// Request confirmation from the receiver.
        /// </summary>
        /// <remarks>In this case a IResponse command should be send back to requesting side. Otherwise client will rise an error after timeout.</remarks>
        public bool Confirm { get; set; }
    }

    public interface IRequest<TResponse> : IRequest
        where TResponse : IResponse
    {
    }
}
