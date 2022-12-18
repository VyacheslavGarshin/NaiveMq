using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Commands
{
    public class LeaveCluster : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }
    }
}