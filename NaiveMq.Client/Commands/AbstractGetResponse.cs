using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractGetResponse<T> : AbstractResponse<AbstractGetResponse<T>>
    {
        [DataMember(Name = "E")]
        public T Entity { get; set; }
    }
}
