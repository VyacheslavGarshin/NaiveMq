using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Common;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class ClientContext : IDisposable
    {
        public Storage Storage { get; set; }

        public NaiveMqClient Client { get; set; }

        public ILogger Logger { get; set; }

        public CancellationToken StoppingToken { get; set; }

        public UserCog User { get; set; }

        public ConcurrentDictionary<QueueCog, SubscriptionCog> Subscriptions { get; } = new();

        /// <summary>
        /// True in case handler is called on reinstating persistent data.
        /// </summary>
        public bool Reinstate { get; set; }
        
        public void CheckUser(ClientContext context)
        {
            CheckUser(context, false);
        }

        public void CheckAdmin(ClientContext context)
        {
            CheckUser(context, true);
        }

        private void CheckUser(ClientContext context, bool checkAdmin)
        {
            if (context.User == null || string.IsNullOrWhiteSpace(context.User.Entity.Username))
            {
                throw new ServerException(ErrorCode.UserNotAuthenticated);
            }

            if (!Storage.Users.TryGetValue(context.User.Entity.Username, out var _))
            {
                throw new ServerException(ErrorCode.UserNotFound, new object[] { context.User.Entity.Username });
            }

            if (checkAdmin && !context.User.Entity.Administrator)
            {
                throw new ServerException(ErrorCode.AccessDeniedNotAdmin);
            }
        }

        public void Dispose()
        {
            foreach (var subscription in Subscriptions.Values)
            {
                subscription.Dispose();
            }

            Subscriptions.Clear();

            Client.Dispose();
        }
    }
}
