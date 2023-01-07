namespace NaiveMq.Client.Enums
{
    /// <summary>
    /// Strategy for the queue subscribed to the cluster server if queue is empty for the specified time.
    /// </summary>
    public enum ClusterStrategy
    {
        /// <summary>
        /// Proxy messages to client from the other cluster node.
        /// </summary>
        /// <remarks>Default. Good for client with several subscriptions.</remarks>
        Proxy = 0,

        /// <summary>
        /// Redirect client to the other cluster node.
        /// </summary>
        /// <remarks>Ideal for the client with one subscription.</remarks>
        Redirect = 1,

        /// <summary>
        /// Send a hint message to client with servers statistics to deal with message shortage manually.
        /// </summary>
        Hint = 2,

        /// <summary>
        /// Wait for the new messages to appear.
        /// </summary>
        Wait = 3,
    }
}