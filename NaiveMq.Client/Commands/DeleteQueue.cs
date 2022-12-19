namespace NaiveMq.Client.Commands
{
    public class DeleteQueue : AbstractRequest<Confirmation>, IReplicable
    {
        public string Name { get; set; }

        public DeleteQueue()
        {
        }

        public DeleteQueue(string name)
        {
            Name = name;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Name))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Name) });
            }
        }
    }
}
