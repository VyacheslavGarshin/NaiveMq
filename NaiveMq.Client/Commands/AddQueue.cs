using NaiveMq.Client.Enums;

namespace NaiveMq.Client.Commands
{
    public class AddQueue : AbstractRequest<Confirmation>, IReplicable
    {
        public string Name { get; set; }

        public bool Durable { get; set; }
        
        public bool Exchange { get; set; }

        public long? LengthLimit { get; set; }

        public long? VolumeLimit { get; set; }

        public LimitStrategy LimitStrategy { get; set; } = LimitStrategy.Delay;

        public AddQueue()
        {
        }

        public AddQueue(string name, bool durable = false, bool exchange = false, long? lengthLimit = null, long? volumeLimit = null, LimitStrategy limitStrategy = LimitStrategy.Delay)
        {
            Name = name;
            Durable = durable;
            Exchange = exchange;
            LengthLimit = lengthLimit;
            VolumeLimit = volumeLimit;
            LimitStrategy = limitStrategy;
        }

        public override void Validate()
        {
            base.Validate();

            if (string.IsNullOrEmpty(Name))
            {
                throw new ClientException(ErrorCode.ParameterNotSet, new[] { nameof(Name) });
            }

            if (LengthLimit != null && LengthLimit.Value < 1)
            {
                throw new ClientException(ErrorCode.ParameterLessThan, new object[] { nameof(LengthLimit), 1 });
            }

            if (VolumeLimit != null && VolumeLimit.Value < 1)
            {
                throw new ClientException(ErrorCode.ParameterLessThan, new object[] { nameof(VolumeLimit), 1 });
            }
        }
    }
}
