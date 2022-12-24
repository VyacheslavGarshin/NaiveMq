namespace NaiveMq.Service
{
    public class NaiveMqServiceOptions
    {
        public string Name { get; set; } = Guid.NewGuid().ToString();

        public int Port { get; set; } = 8506;

        public TimeSpan ListenerRecoveryInterval { get; set; } = TimeSpan.FromSeconds(5);

        public long? MemoryLimit { get; set; }

        /// <summary>
        /// Percentage of memory allowed to be taken.
        /// </summary>
        public int AutoMemoryLimitPercent { get; set; } = 90;

        /// <summary>
        /// Forced queue length limit will be set if auto memory limit reached for this percent of current length.
        /// </summary>
        public int AutoQueueLimitPercent { get; set; } = 90;

        public int TrackFailedRequestsLimit = 1000;

        public string ClusterKey { get; set; }

        /// <summary>
        /// Comma/semicolon separated list of host:port values.
        /// </summary>
        public string ClusterHosts { get; set; }

        public TimeSpan ClusterDiscoveryInterval { get; set; } = TimeSpan.FromMinutes(1);

        public string ClusterAdminUsername { get; set; }

        public string ClusterAdminPassword { get; set; }

        public TimeSpan ClusterStatsInterval { get; set; } = TimeSpan.FromSeconds(10);

        public int ClusterStatsBatchSize { get; set; } = 100;
    }
}