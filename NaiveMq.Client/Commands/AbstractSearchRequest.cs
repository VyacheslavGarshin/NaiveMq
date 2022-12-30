using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractSearchRequest<TResponse> : AbstractRequest<TResponse>
        where TResponse : IResponse
    {
        [DataMember(Name = "Ta")]
        public int Take { get; set; } = 10;

        [DataMember(Name = "S")]
        public int Skip { get; set; }

        [DataMember(Name = "Co")]
        public bool Count { get; set; }
    }
}
