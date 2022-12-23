namespace NaiveMq.Client.Commands
{
    public class ClusterRedirect : AbstractRequest<Confirmation>
    {
        public string Host { get; set; }

        public ClusterRedirect()
        {
        }

        public ClusterRedirect(string host)
        {
            Host = host;
        }

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
