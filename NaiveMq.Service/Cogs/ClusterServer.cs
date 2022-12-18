using NaiveMq.Client;

namespace NaiveMq.Service.Cogs
{
    public class ClusterServer
    {
        public string Name { get; set; }

        public bool Self { get; set; }

        public NaiveMqClient Client { get; set; }
    }
}
