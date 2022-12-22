namespace NaiveMq.Service.Dto
{
    public class QueueStats
    {
        public string Key => $"{User}:{Name}";

        public string User { get; set; }

        public string Name { get; set; }

        public long Length { get; set; }

        public long Subscriptions { get; set; }

        public bool Outdated { get; set; }

        public QueueStats()
        {
        }

        public QueueStats(string user, string name, long length, long subscriptions)
        {
            User = user;
            Name = name;
            Length = length;
            Subscriptions = subscriptions;
        }
    }
}
