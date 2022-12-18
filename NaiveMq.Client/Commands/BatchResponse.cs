using System.Collections.Generic;

namespace NaiveMq.Client.Commands
{
    public class BatchResponse : AbstractResponse<BatchResponse>
    {
        public List<MessageResponse> Responses { get; set; }

        public BatchResponse()
        {
        }

        public BatchResponse(List<MessageResponse> responses)
        {
            Responses = responses;
        }
    }
}
