using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Commands
{
    // todo implement
    public class JoinCluster : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }
    }
}