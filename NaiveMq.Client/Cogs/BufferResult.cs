namespace NaiveMq.Client.Cogs
{
    /// <summary>
    /// Buffer result.
    /// </summary>
    public class BufferResult
    {
        /// <summary>
        /// Buffer.
        /// </summary>
        public byte[] Buffer { get; set; }

        /// <summary>
        /// Actual result length written to the buffer.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Creates new BufferResult.
        /// </summary>
        public BufferResult()
        {
        }

        /// <summary>
        /// Creates new BufferResult.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        public BufferResult(byte[] buffer, int length)
        {
            Buffer = buffer;
            Length = length;
        }
    }
}
