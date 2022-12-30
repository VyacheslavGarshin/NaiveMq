using System.Runtime.Serialization;

namespace NaiveMq.Service.Dto
{
    public class ActiveQueue
    {
        public string Key => CreateKey(User, Name);

        [DataMember(Name = "U")]
        public string User { get; set; }

        [DataMember(Name = "N")]
        public string Name { get; set; }

        [DataMember(Name = "L")]
        public long Length { get; set; }

        [DataMember(Name = "S")]
        public long Subscriptions { get; set; }

        [DataMember(Name = "O")]
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
