using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class Track : AbstractRequest<TrackResponse>
    {
        [DataMember(Name = "S")]
        public bool Start { get; set; }

        [DataMember(Name = "F")]
        public bool Finish { get; set; }

        public Track()
        {
        }

        public Track(bool start, bool finish)
        {
            Start = start;
            Finish = finish;
        }
    }
}
