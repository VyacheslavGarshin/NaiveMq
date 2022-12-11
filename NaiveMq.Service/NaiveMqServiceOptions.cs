namespace NaiveMq.Service
{
    public class NaiveMqServiceOptions
    {
        public int Port { get; set; } = 8506;

        public long? MemoryLimit { get; set; }
    }
}