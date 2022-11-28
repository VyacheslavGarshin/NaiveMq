namespace NaiveMq.Client.Entities
{
    public class QueueEntity
    {
        public string User { get; set; }

        public string Name { get; set; }

        public bool Durable { get; set; }

        public bool IsExchange { get; set; }
    }
}
