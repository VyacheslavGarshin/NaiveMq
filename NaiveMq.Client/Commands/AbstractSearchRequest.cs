namespace NaiveMq.Client.Commands
{
    public abstract class AbstractSearchRequest<TResponse> : AbstractRequest<TResponse>
        where TResponse : IResponse
    {
        public int Take { get; set; } = 10;

        public int Skip { get; set; }

        public bool Count { get; set; }
    }
}
