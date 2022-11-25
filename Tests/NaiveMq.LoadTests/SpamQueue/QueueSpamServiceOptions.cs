namespace NaiveMq.LoadTests.SpamQueue
{
    public class QueueSpamServiceOptions
    {
        public string Host { get; set; }

        public int Port { get; set; }

        public bool Durable { get; set; }

        public int ThreadsCount { get; set; } = 1;

        public int MessageCount { get; set; } = 10;

        public bool Confirm { get; set; }

        public int Runs { get; set; } = 1;

        public bool Subscribe { get; set; }

        public int MessageLength { get; set; } = 100;

        public bool AddQueue { get; set; } = true;

        public bool DeleteQueue { get; set; } = true;

        public string QueueName { get; set; } = "test";
        
        public bool RewriteQueue { get; set; }
        
        public bool ConfirmSubscription { get; set; }
    }
}