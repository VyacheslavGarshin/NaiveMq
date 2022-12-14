using System.Collections.Generic;

namespace NaiveMq.Client.Commands
{
    public class BatchResponse : AbstractResponse<BatchResponse>
    {
        public List<Confirmation> Confirmations { get; set; }
    }
}
