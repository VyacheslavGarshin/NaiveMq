using NaiveMq.Client.Enums;

namespace NaiveMq.Client.Commands
{
    public class AddQueue : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }

        public bool Durable { get; set; }
        
        public bool Exchange { get; set; }

        public long? Limit { get; set; }

        public LimitBy LimitBy { get; set; } = LimitBy.Length;

        public LimitStrategy LimitStrategy { get; set; } = LimitStrategy.Delay;

        public override void Validate()
        {
            base.Validate();

            if (Limit != null && Limit.Value < 1)
            {
                throw new ClientException(ErrorCode.QueueLimitLessThanOne);
            }
        }
    }
}
