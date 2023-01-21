using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NaiveMq.Client.AbstractCommands
{
    /// <summary>
    /// Abstract response for any search command.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class AbstractSearchResponse<T> : AbstractResponse<AbstractSearchResponse<T>>
    {
        /// <summary>
        /// Found enitties.
        /// </summary>
        [DataMember(Name = "E")]
        public List<T> Entities { get; set; }

        /// <summary>
        /// Count of all items if <see cref="AbstractSearchRequest{TResponse}.Count"/> is set to true.
        /// </summary>
        [DataMember(Name = "C")]
        public int? Count { get; set; }
    }
}
