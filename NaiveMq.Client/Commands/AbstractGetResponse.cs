namespace NaiveMq.Client.Commands
{
    public abstract class AbstractGetResponse<T> : AbstractResponse<AbstractGetResponse<T>>
    {
        public T Entity { get; set; }
    }
}
