using NaiveMq.Client.Enums;

namespace NaiveMq.Client.Commands
{
    public class AddQueue : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }

        public bool Durable { get; set; }
        
        public bool Exchange { get; set; }

        public long? LengthLimit { get; set; }

        public long? VolumeLimit { get; set; }

        public LimitStrategy LimitStrategy { get; set; } = LimitStrategy.Delay;

        public override void Validate()
        {
            base.Validate();

            if ((LengthLimit != null && LengthLimit.Value < 1) || (VolumeLimit != null && VolumeLimit.Value < 1))
            {
                throw new ClientException(ErrorCode.QueueLimitLessThanOne);
            }
        }
    }
}
