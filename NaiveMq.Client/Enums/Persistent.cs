namespace NaiveMq.Client.Enums
{
    public enum Persistent
    {
        /// <summary>
        /// Message is stored only in memory.
        /// </summary>
        No = 0,

        /// <summary>
        /// Message is stored in memory and disk.
        /// </summary>
        Yes = 1,

        /// <summary>
        /// Message data is stored only on disk.
        /// </summary>
        /// <remarks>Purpose is to support long queues of big messages.</remarks>
        DiskOnly = 2,
    }
}
