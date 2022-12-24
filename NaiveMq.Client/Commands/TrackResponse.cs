using System;
using System.Collections.Generic;

namespace NaiveMq.Client.Commands
{
    public class TrackResponse : AbstractResponse<TrackResponse>
    {
        public List<Guid> FailedRequests { get; set; }

        public string LastErrorCode { get; set; }

        public string LastErrorMessage { get; set; }
        
        public bool Overflow { get; set; }

        public TrackResponse()
        {
        }

        public TrackResponse(List<Guid> failedRequests = null, string lastErrorCode = null, string lastErrorMessage = null, bool overflow = false)
        {
            FailedRequests = failedRequests;
            LastErrorCode = lastErrorCode;
            LastErrorMessage = lastErrorMessage;
            Overflow = overflow;
        }
    }
}
