using System.Runtime.Serialization;
using NaiveMq.Client.AbstractCommands;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Track requests. On finish <see cref="TrackResponse"/> will be send with failed requests.
    /// </summary>
    /// <remarks>Allows to turn off confirmations and gather errors for the sent batch.</remarks>
    public class Track : AbstractRequest<TrackResponse>
    {
        /// <summary>
        /// Start a new track.
        /// </summary>
        [DataMember(Name = "S")]
        public bool Start { get; set; }

        /// <summary>
        /// Finish the track and get errors for the batch.
        /// </summary>
        [DataMember(Name = "F")]
        public bool Finish { get; set; }

        /// <summary>
        /// Creates new Track command.
        /// </summary>
        public Track()
        {
        }

        /// <summary>
        /// Creates new Track command with params.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="finish"></param>
        public Track(bool start, bool finish)
        {
            Start = start;
            Finish = finish;
        }
    }
}
