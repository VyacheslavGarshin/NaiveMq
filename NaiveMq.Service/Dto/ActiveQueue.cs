namespace NaiveMq.Service.Dto
{
    public class ActiveQueue
    {
        public string Key => CreateKey(User, Name);

        public string User { get; set; }

        public string Name { get; set; }

        public long Length { get; set; }

        public long Subscriptions { get; set; }

        public bool Outdated { get; set; }

        public ActiveQueue()
        {
        }

        public ActiveQueue(string user, string name, long length, long subscriptions)
        {
            User = user;
            Name = name;
            Length = length;
            Subscriptions = subscriptions;
        }

        public static string CreateKey(string user, string queue)
        {
            return $"{user}:{queue}";
        }
    }
}
