namespace NaiveMq.Client.Commands
{
    public class Track : AbstractRequest<TrackResponse>
    {
        public bool Start { get; set; }

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
