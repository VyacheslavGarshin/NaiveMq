using NaiveMq.Client.Enums;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class AddQueue : AbstractRequest<Confirmation>, IReplicable
    {
        [DataMember(Name = "N")]
        public string Name { get; set; }

        [DataMember(Name = "D")]
        public bool Durable { get; set; }

        [DataMember(Name = "E")]
        public bool Exchange { get; set; }

        [DataMember(Name = "LL")]
        public long? LengthLimit { get; set; }

        [DataMember(Name = "VL")]
        public long? VolumeLimit { get; set; }

        [DataMember(Name = "LS")]
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
