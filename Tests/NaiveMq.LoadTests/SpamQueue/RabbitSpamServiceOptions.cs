namespace NaiveMq.LoadTests.SpamQueue
{
    public class RabbitSpamServiceOptions
    {
        public bool IsEnabled { get; set; }

        public string QueueName { get; set; } = "test";

        public bool Durable { get; set; }

        public int ThreadsCount { get; set; } = 1;

        public int MessageCount { get; set; } = 1000000;

        public bool Confirm { get; set; }

        public int Runs { get; set; } = 1;

        public bool Subscribe { get; set; }

        public bool AutoAck { get; set; } = true;

        public int MessageLength { get; set; } = 100;

        public bool ReadBody { get; set; }
    }
}