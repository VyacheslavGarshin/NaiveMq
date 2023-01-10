using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Redirect command for client from the classter to the active node.
    /// </summary>
    public class ClusterRedirect : AbstractRequest<Confirmation>
    {
        /// <summary>
        /// Host.
        /// </summary>
        [DataMember(Name = "H")]
        public string Host { get; set; }

        /// <summary>
        /// Creates new ClusterRedirect command.
        /// </summary>
        public ClusterRedirect()
        {
        }

        /// <summary>
        /// Creates new ClusterRedirect command with params.
        /// </summary>
        /// <param name="host"></param>
        public ClusterRedirect(string host)
        {
            Host = host;
        }

        /// <inheritdoc/>
        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Host))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Host) });
            }
        }
    }
}
