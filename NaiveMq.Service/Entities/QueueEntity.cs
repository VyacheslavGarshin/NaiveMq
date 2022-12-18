using NaiveMq.Client.Commands;
using NaiveMq.Client.Enums;

namespace NaiveMq.Service.Entities
{
    public class QueueEntity
    {
        public string User { get; set; }

        public string Name { get; set; }

        public bool Durable { get; set; }

        public bool Exchange { get; set; }

        public long? LengthLimit { get; set; }

        public long? VolumeLimit { get; set; }

        public LimitStrategy LimitStrategy { get; set; }

        public static QueueEntity FromCommand(AddQueue command)
        {
            return new QueueEntity
            {
                Name = command.Name,
                Durable = command.Durable,
                Exchange = command.Exchange,
                LengthLimit = command.LengthLimit,
                VolumeLimit = command.VolumeLimit,
                LimitStrategy = command.LimitStrategy,
            };
        }
    }
}
