using NaiveMq.Client.AbstractCommands;
using NaiveMq.Client.Enums;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Add new queue.
    /// </summary>
    public class AddQueue : AbstractAddRequest<Confirmation>, IReplicable
    {
        /// <summary>
        /// Name.
        /// </summary>
        [DataMember(Name = "N")]
        public string Name { get; set; }

        /// <summary>
        /// Save.
        /// </summary>
        [DataMember(Name = "D")]
        public bool Durable { get; set; }

        /// <summary>
        /// Mark as exchange.
        /// </summary>
        [DataMember(Name = "E")]
        public bool Exchange { get; set; }

        /// <summary>
        /// Limit by length.
        /// </summary>
        [DataMember(Name = "LL")]
        public long? LengthLimit { get; set; }

        /// <summary>
        /// Limit by volume in bytes.
        /// </summary>
        [DataMember(Name = "VL")]
        public long? VolumeLimit { get; set; }

        /// <summary>
        /// Limit strategy.
        /// </summary>
        [DataMember(Name = "LS")]
        public LimitStrategy LimitStrategy { get; set; } = LimitStrategy.Delay;

        /// <summary>
        /// Creates new AddQueue command.
        /// </summary>
        public AddQueue()
        {
        }

        /// <summary>
        /// Creates new AddQueue command with params.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="durable"></param>
        /// <param name="exchange"></param>
        /// <param name="lengthLimit"></param>
        /// <param name="volumeLimit"></param>
        /// <param name="limitStrategy"></param>
        /// <param name="try"></param>
        public AddQueue(
            string name, 
            bool durable = false, 
            bool exchange = false, 
            long? lengthLimit = null, 
            long? volumeLimit = null, 
            LimitStrategy limitStrategy = LimitStrategy.Delay, 
            bool @try = false) : base(@try)
        {
            Name = name;
            Durable = durable;
            Exchange = exchange;
            LengthLimit = lengthLimit;
            VolumeLimit = volumeLimit;
            LimitStrategy = limitStrategy;
        }

        /// <inheritdoc/>
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
