using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Commands
{
    // todo implement
    public class LeaveCluster : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }
    }
}