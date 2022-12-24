using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class ClientContext : IDisposable
    {
        public Storage Storage { get; set; }

        public NaiveMqClient Client { get; set; }

        public ILogger Logger { get; set; }

        public UserCog User { get; set; }

        public ConcurrentDictionary<QueueCog, SubscriptionCog> Subscriptions { get; } = new();

        public bool Tracking { get; set; }

        public ConcurrentBag<Guid> TrackFailedRequests { get; set; } = new();

        public string TrackLastErrorCode { get; set; }

        public string TrackLastErrorMessage { get; set; }

        public bool TrackOverflow { get; set; }

        /// <summary>
        /// True in case handler is called on reinstating persistent data.
        /// </summary>
        public bool Reinstate { get; set; }
        
        public void CheckUser(ClientContext context)
        {
            if (context.User == null || string.IsNullOrWhiteSpace(context.User.Entity.Username))
            {
                throw new ServerException(ErrorCode.UserNotAuthenticated);
            }

            if (!Storage.Users.TryGetValue(context.User.Entity.Username, out var _))
            {
                throw new ServerException(ErrorCode.UserNotFound, new object[] { context.User.Entity.Username });
            }
        }

        public void CheckAdmin(ClientContext context)
        {
            CheckUser(context);

            if (!context.User.Entity.Administrator)
            {
                throw new ServerException(ErrorCode.AccessDeniedNotAdmin);
            }
        }

        public void CheckClusterAdmin(ClientContext context)
        {
            CheckAdmin(context);

            if (!context.User.Entity.Username.Equals(Storage.Service.Options.ClusterAdminUsername, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ServerException(ErrorCode.AccessDeniedNotClusterAdmin);
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
