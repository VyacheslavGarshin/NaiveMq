namespace NaiveMq.Client.Commands
{
    public class DeleteBinding : AbstractRequest<Confirmation>
    {
        public string Exchange { get; set; }

        public string Queue { get; set; }

        public DeleteBinding()
        {
        }

        public DeleteBinding(string exchange, string queue)
        {
            Exchange = exchange;
            Queue = queue;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Exchange))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Exchange) });
            }

            if (string.IsNullOrEmpty(Queue))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Queue) });
            }
        }
    }
}
