using NaiveMq.Client.Commands;

namespace NaiveMq.Service.Commands
{
    public class JoinCluster : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }
    }
}