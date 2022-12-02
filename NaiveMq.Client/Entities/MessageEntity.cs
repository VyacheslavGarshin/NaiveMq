using System;

namespace NaiveMq.Client.Entities
{
    public class MessageEntity
    {
        public Guid Id { get; set; }

        public int ClientId { get; set; }

        public string Queue { get; set; }

        public bool Request { get; set; }

        public bool Durable { get; set; }

        public string BindingKey { get; set; }

        public string Text { get; set; }
    }
}
