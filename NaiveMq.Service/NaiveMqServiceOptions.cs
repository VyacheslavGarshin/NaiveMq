namespace NaiveMq.Service
{
    public class NaiveMqServiceOptions
    {
        public int Port { get; set; } = 8506;

        public long? MemoryLimit { get; set; }

        /// <summary>
        /// Percentage of memory allowed to be taken.
        /// </summary>
        public int AutoMemoryLimitThreshold { get; set; } = 90;

        /// <summary>
        /// Forced queue length limit will be set if auto memory limit reached for this percent of current length.
        /// </summary>
        public int AutoQueueLimitThreshold { get; set; } = 90;
    }
}