using NaiveMq.Client.Enums;

namespace NaiveMq.Client.Dto
{
    public class Queue
    {
        public string User { get; set; }

        public string Name { get; set; }

        public bool Durable { get; set; }

        public bool Exchange { get; set; }

        public long? Limit { get; set; }

        public LimitType LimitType { get; set; }

        public LimitStrategy LimitStrategy { get; set; }
    }
}
