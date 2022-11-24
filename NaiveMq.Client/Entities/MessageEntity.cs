using System;

namespace NaiveMq.Client.Entities
{
    public class MessageEntity
    {
        public Guid Id { get; set; }

        public string Queue { get; set; }

        public string Text { get; set; }
    }
}
