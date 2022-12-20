namespace NaiveMq.Client.Enums
{
    public enum ClusterStrategy
    {
        // todo implemetn proxy in subscribtioncog
        /// <summary>
        /// Proxy messages to client from the other cluster node.
        /// </summary>
        Proxy = 0,

        // todo implement redirection in cog and client
        /// <summary>
        /// Redirect client to the other cluster node.
        /// </summary>
        Redirect = 1,

        // todo implement sending hint to client
        /// <summary>
        /// Send a hint message to client with servers statistics to deal with message shortage manually.
        /// </summary>
        Hint = 2,
    }
}