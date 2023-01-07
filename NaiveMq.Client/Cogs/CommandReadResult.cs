namespace NaiveMq.Client.Cogs
{
    /// <summary>
    /// Result of the reading command from the stream into buffer.
    /// </summary>
    public class CommandReadResult
    {
        /// <summary>
        /// Buffer.
        /// </summary>
        public byte[] Buffer { get; set; }

        /// <summary>
        /// Command name length.
        /// </summary>
        public int CommandNameLength { get; set; }

        /// <summary>
        /// Command length.
        /// </summary>
        public int CommandLength { get; set; }

        /// <summary>
        /// Data length.
        /// </summary>
        public int DataLength { get; set; }
    }
}
