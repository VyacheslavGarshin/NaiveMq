namespace NaiveMq.Client.Commands
{
    public class AbstractGetResponse<T> : AbstractResponse<AbstractGetResponse<T>>
    {
        public T Entity { get; set; }
    }
}
