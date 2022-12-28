using Microsoft.Extensions.Logging;
using NaiveMq.Client;

namespace NaiveMq.Service.Cogs
{
    public class NaiveMqClientWithContext : NaiveMqClient
    {
        public ClientContext Context { get; }

        public NaiveMqClientWithContext(
            NaiveMqClientOptions options,
            ClientContext context,
            ILogger<NaiveMqClient> logger, 
            CancellationToken stoppingToken) : base(options, logger, stoppingToken)
        {
            Context = context;
            Context.Client = this;
        }

        public override void Dispose()
        {
            base.Dispose();

            Context.Dispose();
        }
    }
}
