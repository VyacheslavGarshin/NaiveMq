using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractSearchResponse<T> : AbstractResponse<AbstractSearchResponse<T>>
    {
        [DataMember(Name = "E")]
        public List<T> Entities { get; set; }

        [DataMember(Name = "C")]
        public int? Count { get; set; }
    }
}
