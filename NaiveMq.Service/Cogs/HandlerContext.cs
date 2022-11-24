using Microsoft.Extensions.Logging;
using NaiveMq.Client;

namespace NaiveMq.Service.Cogs
{
    public class HandlerContext
    {
        public Storage Storage { get; set; }

        public NaiveMqClient Client { get; set; }

        public ILogger Logger { get; set; }

        public CancellationToken CancellationToken { get; set; }
        
        public string User { get; set; }

        /// <summary>
        /// In case handler is called on reinstating persistent messages.
        /// </summary>
        public bool Reinstate { get; set; }
    }
}
