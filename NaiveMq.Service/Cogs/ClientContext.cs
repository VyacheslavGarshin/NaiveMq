using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Service.Enums;
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

        public ClientContextMode Mode { get; set; }

        public void CheckUser()
        {
            if (User == null || string.IsNullOrWhiteSpace(User.Entity.Username))
            {
                throw new ServerException(ErrorCode.UserNotAuthenticated);
            }

            if (!Storage.Users.TryGetValue(User.Entity.Username, out var _))
            {
                throw new ServerException(ErrorCode.UserNotFound, new object[] { User.Entity.Username });
            }
        }

        public void CheckAdmin()
        {
            CheckUser();

            if (!User.Entity.Administrator)
            {
                throw new ServerException(ErrorCode.AccessDeniedNotAdmin);
            }
        }

        public void CheckClusterAdmin()
        {
            CheckAdmin();

            if (!User.Entity.Username.Equals(Storage.Service.Options.ClusterAdminUsername, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ServerException(ErrorCode.AccessDeniedNotClusterAdmin);
            }
        }

        public void Dispose()
        {
            if (Mode == ClientContextMode.Client)
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
}
