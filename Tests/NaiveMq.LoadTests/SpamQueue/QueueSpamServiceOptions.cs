using NaiveMq.Client.Enums;

namespace NaiveMq.LoadTests.SpamQueue
{
    public class QueueSpamServiceOptions
    {
        public bool IsEnabled { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string Hosts { get; set; }

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

        public int QueueCount { get; set; } = 1;

        public bool RewriteQueue { get; set; }
        
        public bool ConfirmSubscription { get; set; }

        public string GetUser { get; set; }

        public bool GetUserTry { get; set; }

        public string AddUser { get; set; }
        
        public bool DeleteUser { get; set; }

        public string UpdateUser { get; set; }

        public string SearchUsers { get; set; }

        public string SearchQueues { get; set; }

        public string ChangePassword { get; set; }

        public bool GetProfile { get; set; }

        public string Exchange { get; set; }

        public string ExchangeTo { get; set; }        

        public bool AddExchange { get; set; }

        public bool AddBinding { get; set; }

        public string BindingPattern { get; set; }

        public bool DeleteBinding { get; set; }

        public string SendExchangeMessageWithKey { get; set; }

        public TimeSpan? ConfirmMessageTimeout { get; set; }

        public bool Request { get; set; }

        public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromSeconds(2);

        public Persistence PersistentMessage { get; set; } = Persistence.No;

        public int Parallelism { get; set; } = 8;

        public TimeSpan? ReceiveDelay { get; set; }

        public TimeSpan? SendDelay { get; set; }

        public bool LogClientCounters { get; set; }

        public long? Limit { get; set; }

        public LimitBy LimitBy { get; set; }

        public LimitStrategy LimitStrategy { get; set; }

        public bool ReadBody { get; set; }

        public bool Batch { get; set; }

        public int BatchSize { get; set; } = 10;

        public bool Wait { get; set; } = true;

        public bool ClearQueue { get; set; } = false;
    }
}