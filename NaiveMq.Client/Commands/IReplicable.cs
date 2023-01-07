namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Any command with this interface will be replicated across the cluster.
    /// </summary>
    public interface IReplicable
    {
    }
}
