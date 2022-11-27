using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Common;
using NaiveMq.Client.Entities;

namespace NaiveMq.Service.Cogs
{
    public class ClientContext
    {
        public Storage Storage { get; set; }

        public NaiveMqClient Client { get; set; }

        public ILogger Logger { get; set; }

        public CancellationToken CancellationToken { get; set; }

        public UserEntity User { get; set; }

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
            if (context.User == null || string.IsNullOrWhiteSpace(context.User.Username))
            {
                throw new ServerException(ErrorCode.UserNotAuthenticated, ErrorCode.UserNotAuthenticated.GetDescription());
            }

            if (!Storage.Users.TryGetValue(context.User.Username, out var _))
            {
                throw new ServerException(ErrorCode.UserNotFound, string.Format(ErrorCode.UserNotFound.GetDescription(), context.User.Username));
            }

            if (checkAdmin && !context.User.IsAdministrator)
            {
                throw new ServerException(ErrorCode.AccessDeniedNotAdmin, ErrorCode.AccessDeniedNotAdmin.GetDescription());
            }
        }
    }
}
