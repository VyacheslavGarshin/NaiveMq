using NaiveMq.Client.Enums;

namespace NaiveMq.Service.Entities
{
    public class QueueEntity
    {
        public string User { get; set; }

        public string Name { get; set; }

        public bool Durable { get; set; }

        public bool Exchange { get; set; }

        public long? Limit { get; set; }

        public LimitBy LimitBy { get; set; }

        public LimitStrategy LimitStrategy { get; set; }
    }
}
