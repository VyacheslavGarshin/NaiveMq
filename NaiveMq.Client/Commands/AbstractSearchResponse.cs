using System.Collections.Generic;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractSearchResponse<T> : AbstractResponse<AbstractSearchResponse<T>>
    {
        public List<T> Entities { get; set; }

        public int? Count { get; set; }
    }
}
