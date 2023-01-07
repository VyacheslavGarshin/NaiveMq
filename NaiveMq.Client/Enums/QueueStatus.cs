namespace NaiveMq.Client.Enums
{
    /// <summary>
    /// Queue status.
    /// </summary>
    public enum QueueStatus
    {
        /// <summary>
        /// Starting.
        /// </summary>
        Starting = 0,

        /// <summary>
        /// Started. The only status when a queue receiving and sending messages.
        /// </summary>
        Started = 1,

        /// <summary>
        /// Clearing.
        /// </summary>
        Clearing = 2,

        /// <summary>
        /// Deleting.
        /// </summary>
        Deleting = 3,

        /// <summary>
        /// Deleted.
        /// </summary>
        Deleted = 4
    }
}
