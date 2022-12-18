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
    }
}
