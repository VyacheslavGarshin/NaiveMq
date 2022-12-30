using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class TrackResponse : AbstractResponse<TrackResponse>
    {
        [DataMember(Name = "FR")]
        public List<Guid> FailedRequests { get; set; }

        [DataMember(Name = "LEC")]
        public string LastErrorCode { get; set; }

        [DataMember(Name = "LEM")]
        public string LastErrorMessage { get; set; }

        [DataMember(Name = "O")]
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
