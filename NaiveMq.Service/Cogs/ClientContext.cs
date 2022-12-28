using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Enums;
using NaiveMq.Service.Handlers;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class ClientContext : IDisposable
    {
        public Storage Storage { get; set; }

        public NaiveMqClientWithContext Client { get; set; }

        public ILogger Logger { get; set; }

        public UserCog User { get; set; }

        public ConcurrentDictionary<QueueCog, SubscriptionCog> Subscriptions { get; } = new();

        public QueueCog LastQueue { get; set; }

        public Type LastHandlerType { get; set; }

        public bool Tracking { get; set; }

        public ConcurrentBag<Guid> TrackFailedRequests { get; set; } = new();

        public string TrackLastErrorCode { get; set; }

        public string TrackLastErrorMessage { get; set; }

        public bool TrackOverflow { get; set; }

        public ClientContextMode Mode { get; set; }

        public void CheckUser()
        {
            if (User == null)
            {
                throw new ServerException(ErrorCode.UserNotAuthenticated);
            }

            if (User.Status == UserStatus.Deleted)
            {
                throw new ServerException(ErrorCode.UserNotFound, new[] { User.Entity.Username });
            }
            else if (User.Status != UserStatus.Started)
            {
                throw new ServerException(ErrorCode.UserNotStarted, new[] { User.Entity.Username });
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
            }

            Client = null;
            LastQueue = null;
        }
    }
}
