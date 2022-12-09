using NaiveMq.Client.Enums;

namespace NaiveMq.Client.Commands
{
    public class AddQueue : AbstractRequest<Confirmation>
    {
        public string Name { get; set; }

        public bool Durable { get; set; }
        
        public bool Exchange { get; set; }

        public long? Limit { get; set; }

        public LimitType LimitType { get; set; }

        public LimitStrategy LimitStrategy { get; set; }
    }
}
