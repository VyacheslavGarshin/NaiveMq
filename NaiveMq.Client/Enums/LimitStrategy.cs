namespace NaiveMq.Client.Enums
{
    /// <summary>
    /// Limit strategy.
    /// </summary>
    public enum LimitStrategy
    {
        /// <summary>
        /// Delay message.
        /// </summary>
        Delay = 0,

        /// <summary>
        /// Reject message.
        /// </summary>
        Reject = 1,        

        /// <summary>
        /// Discart message.
        /// </summary>
        Discard = 2,
    }
}