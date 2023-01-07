using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Track response.
    /// </summary>
    public class TrackResponse : AbstractResponse<TrackResponse>
    {
        /// <summary>
        /// Failed requests for the track.
        /// </summary>
        [DataMember(Name = "FR")]
        public List<Guid> FailedRequests { get; set; }

        /// <summary>
        /// Last error code.
        /// </summary>
        [DataMember(Name = "LEC")]
        public string LastErrorCode { get; set; }

        /// <summary>
        /// Last error message.
        /// </summary>
        [DataMember(Name = "LEM")]
        public string LastErrorMessage { get; set; }

        /// <summary>
        /// Number of errors was more than server limit for the stored list of errors (default 1000).
        /// </summary>
        [DataMember(Name = "O")]
        public bool Overflow { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public TrackResponse()
        {
        }

        /// <summary>
        /// Constructor with params.
        /// </summary>
        /// <param name="failedRequests"></param>
        /// <param name="lastErrorCode"></param>
        /// <param name="lastErrorMessage"></param>
        /// <param name="overflow"></param>
        public TrackResponse(List<Guid> failedRequests = null, string lastErrorCode = null, string lastErrorMessage = null, bool overflow = false)
        {
            FailedRequests = failedRequests;
            LastErrorCode = lastErrorCode;
            LastErrorMessage = lastErrorMessage;
            Overflow = overflow;
        }
    }
}
