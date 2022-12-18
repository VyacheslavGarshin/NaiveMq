namespace NaiveMq.Service.Cogs
{
    public class ClusterServer
    {
        public string Name { get; set; }

        public bool Self { get; internal set; }

        public ClientContext ClientContext { get; set; }
    }
}
