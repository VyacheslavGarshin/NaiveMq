namespace NaiveMq.Client.Common
{
    public class ReadResult
    {
        public byte[] Buffer { get; set; }
        public int CommandNameLength { get; set; }
        public int CommandLength { get; set; }
        public int DataLength { get; set; }
    }
}
