namespace NaiveMq.Service
{
    public class NaiveMqServiceOptions
    {
        public int Port { get; set; } = 1245;

        public long? MemoryLimit { get; set; }
    }
}