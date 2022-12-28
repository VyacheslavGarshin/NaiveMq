using NaiveMq.Client;
using NaiveMq.Client.Common;
using NaiveMq.Client.Enums;
using NaiveMq.Service.Counters;
using NaiveMq.Service.Entities;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class UserCog : IDisposable
    {
        public UserEntity Entity { get; set; }

        public UserStatus Status { get; private set; }

        public ConcurrentDictionary<string, QueueCog> Queues { get; } = new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Keys are: from queue, to queue.</remarks>
        public ConcurrentDictionary<string, ConcurrentDictionary<string, BindingCog>> Bindings { get; }  = new(StringComparer.InvariantCultureIgnoreCase);

        private static readonly UserStatus[] _constantStatuses = new UserStatus[] { UserStatus.Started, UserStatus.Deleted };

        private readonly object _statusLocker = new object();

        public UserCounters Counters { get; }

        public UserCog(UserEntity entity, StorageCounters storageCounters, SpeedCounterService speedCounterService)
        {
            Entity = entity;
            Counters = new(speedCounterService, storageCounters);
        }

        public void SetStatus(UserStatus status)
        {
            lock (_statusLocker)
            {
                if (_constantStatuses.Contains(status) || Status == UserStatus.Started)
                {
                    Status = status;
                }
                else
                {
                    throw new ServerException(ErrorCode.UserNotStarted, new[] { Entity.Username });
                }
            }
        }

        public void Dispose()
        {
            foreach (var queue in Queues.Values)
            {
                queue.Dispose();
            }

            Counters.Dispose();
        }
    }
}
