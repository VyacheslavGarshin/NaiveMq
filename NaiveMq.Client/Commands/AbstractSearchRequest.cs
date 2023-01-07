using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Abstract implementation for any search request.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public abstract class AbstractSearchRequest<TResponse> : AbstractRequest<TResponse>
        where TResponse : IResponse
    {
        /// <summary>
        /// Take this number of items.
        /// </summary>
        [DataMember(Name = "Ta")]
        public int Take { get; set; } = 10;

        /// <summary>
        /// Skip this number of items.
        /// </summary>
        [DataMember(Name = "S")]
        public int Skip { get; set; }

        /// <summary>
        /// Calculate and return overall count of found items.
        /// </summary>
        [DataMember(Name = "Co")]
        public bool Count { get; set; }
    }
}
