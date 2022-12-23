namespace NaiveMq.Client.Dto
{
    public class QueueHint
    {
        public string Name { get; set; }

        public string Host { get; set; }

        public long Length { get; set; }

        public long Subscriptions { get; set; }
    }
}
