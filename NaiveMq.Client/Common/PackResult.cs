namespace NaiveMq.Client.Common
{
    public class PackResult
    {
        public byte[] Buffer { get; set; }
        public int Length { get; set; }

        public PackResult()
        {
        }

        public PackResult(byte[] buffer, int length)
        {
            Buffer = buffer;
            Length = length;
        }
    }
}
