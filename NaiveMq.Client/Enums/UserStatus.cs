namespace NaiveMq.Client.Enums
{
    /// <summary>
    /// User status.
    /// </summary>
    public enum UserStatus
    {
        /// <summary>
        /// Starting.
        /// </summary>
        Starting = 0,

        /// <summary>
        /// Started. The only status when a user can perform operations.
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
