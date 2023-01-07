using NaiveMq.Client.Commands;
using System.ComponentModel;

namespace NaiveMq.Client
{
    /// <summary>
    /// Client error code.
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// Command not found.
        /// </summary>
        [Description("Command '{0}' not found.")]
        CommandNotFound = 1,

        /// <summary>
        /// Command Id must be set.
        /// </summary>
        [Description("Command Id must be set.")]
        EmptyCommandId = 2,

        /// <summary>
        /// Client is stopped.
        /// </summary>
        [Description("Client is stopped.")]
        ClientStopped = 3,

        /// <summary>
        /// Confirmation timeout.
        /// </summary>
        [Description("Confirmation timeout.")]
        ConfirmationTimeout = 4,

        /// <summary>
        /// Unknown confirmation error. See 'Response' property of exception for original response.
        /// </summary>
        [Description("Unknown confirmation error. See 'Response' property of exception for original response.")]
        ConfirmationError = 5,

        /// <summary>
        /// Connection parameters are empty.
        /// </summary>
        [Description("Connection parameters are empty.")]
        ConnectionParametersAreEmpty = 6,

        /// <summary>
        /// Command name length cannot exceed <see cref="NaiveMqClientOptions.MaxCommandNameSize"/>.
        /// </summary>
        [Description("Command name length cannot exceed {0}. Current length id '{1}'.")]
        CommandNameLengthLong = 7,

        /// <summary>
        /// Command length cannot exceed <see cref="NaiveMqClientOptions.MaxCommandSize"/>.
        /// </summary>
        [Description("Command length cannot exceed {0}. Current length id '{1}'.")]
        CommandLengthLong = 8,

        /// <summary>
        /// Data length cannot exceed <see cref="NaiveMqClientOptions.MaxDataSize"/>.
        /// </summary>
        [Description("Data length cannot exceed {0}. Current length id '{1}'.")]
        DataLengthLong = 9,

        /// <summary>
        /// <see cref="IResponse.RequestId"/> must be set for the response command.
        /// </summary>
        [Description("RequestId must be set for the response command.")]
        RequestIdNotSet = 10,

        /// <summary>
        /// Parameter must be set.
        /// </summary>
        [Description("Parameter '{0}' must be set.")]
        ParameterNotSet = 11,

        /// <summary>
        /// Batch commands are empty.
        /// </summary>
        [Description("Batch commands are empty.")]
        BatchCommandsEmpty = 12,

        /// <summary>
        /// Batch cannot contain message with <see cref="Message.Request"/> parameter set to true.
        /// </summary>
        [Description("Batch cannot contain message with Request parameter set to true.")]
        BatchContainsRequestMessage = 13,

        /// <summary>
        /// Command already registered.
        /// </summary>
        [Description("Command with name '{0}' already registered.")]
        CommandAlreadyRegistered = 14,

        /// <summary>
        /// Hosts parameter is not set.
        /// </summary>
        [Description("Hosts parameter is not set.")]
        HostsNotSet = 15,

        /// <summary>
        /// Request message cannot be persistent.
        /// </summary>
        [Description("Request cannot be persistent.")]
        PersistentRequest = 16,

        /// <summary>
        /// Message data must be set.
        /// </summary>
        [Description("Message data must be set.")]
        DataIsEmpty = 17,

        /// <summary>
        /// Non of the hosts defined is reachable.
        /// </summary>
        [Description("Non of the hosts defined is reachable.")]
        HostsUnavailable = 18,

        /// <summary>
        /// Request message should be confirmed.
        /// </summary>
        [Description("Request message should be confirmed.")]
        RequestConfirmRequred = 19,

        /// <summary>
        /// Parameter cannot be less than specified.
        /// </summary>
        [Description("Parameter '{0}' cannot be less than {1}.")]
        ParameterLessThan = 21,

        /// <summary>
        /// Cannot find handler for command.
        /// </summary>
        [Description("Cannot find handler for command '{0}'.")]
        CommandHandlerNotFound = 101,

        /// <summary>
        /// Unexpected error executing command.
        /// </summary>
        [Description("Unexpected error executing command. Error: {0}.")]
        UnexpectedCommandHandlerExecutionError = 102,

        /// <summary>
        /// Subscription on queue already exists.
        /// </summary>
        [Description("Subscription on queue '{0}' already exists.")]
        SubscriptionAlreadyExists = 103,

        /// <summary>
        /// Subscription on queue is not found.
        /// </summary>
        [Description("Subscription on queue '{0}' is not found.")]
        SubscriptionNotFound = 104,

        /// <summary>
        /// Queue already exists.
        /// </summary>
        [Description("Queue '{0}' already exists.")]
        QueueAlreadyExists = 105,

        /// <summary>
        /// Queue is not found.
        /// </summary>
        [Description("Queue '{0}' is not found.")]
        QueueNotFound = 106,

        /// <summary>
        /// User already exists.
        /// </summary>
        [Description("User '{0}' already exists.")]
        UserAlreadyExists = 109,

        /// <summary>
        /// You must have administrator rights to perform this operation.
        /// </summary>
        [Description("You must have administrator rights to perform this operation.")]
        AccessDeniedNotAdmin = 110,

        /// <summary>
        /// User is not authenticated.
        /// </summary>
        [Description("User is not authenticated.")]
        UserNotAuthenticated = 112,

        /// <summary>
        /// Username or password is not correct.
        /// </summary>
        [Description("Username or password is not correct.")]
        UserOrPasswordNotCorrect = 113,

        /// <summary>
        /// User is not found.
        /// </summary>
        [Description("User '{0}' is not found.")]
        UserNotFound = 114,

        /// <summary>
        /// Users cannot delete themselves.
        /// </summary>
        [Description("Users cannot delete themselves.")]
        UserDeleteSelf = 115,

        /// <summary>
        /// Users cannot unset administrator privilege on themselves.
        /// </summary>
        [Description("Users cannot unset administrator privilege on themselves.")]
        UserUnsetAdministratorSelf = 116,

        /// <summary>
        /// Wrong password.
        /// </summary>
        [Description("Wrong password.")]
        WrongPassword = 117,

        /// <summary>
        /// New password cannot be the same as an old one.
        /// </summary>
        [Description("New password cannot be the same as an old one.")]
        NewPasswordTheSame = 118,

        /// <summary>
        /// Password сannot be empty.
        /// </summary>
        [Description("Password сannot be empty.")]
        PasswordEmpty = 119,

        /// <summary>
        /// Exchange is already bound to queue.
        /// </summary>
        [Description("Exchange '{0}' is already bound to queue '{1}'.")]
        BindingAlreadyExists = 121,

        /// <summary>
        /// Exchange is not found.
        /// </summary>
        [Description("Exchange '{0}' is not found.")]
        ExchangeNotFound = 122,

        /// <summary>
        /// Durable binding must bind both durable exchange and queue.
        /// </summary>
        [Description("Durable binding must bind both durable exchange and queue.")]
        DurableBindingCheck = 123,

        /// <summary>
        /// Cannot bind exchange.
        /// </summary>
        [Description("Cannot bind exchange.")]
        BindExchange = 124,

        /// <summary>
        /// Cannot bind to a queue which is not exchange.
        /// </summary>
        [Description("Cannot bind to a queue which is not exchange.")]
        BindToQueue = 125,

        /// <summary>
        /// Binding of exchange and queue is not found.
        /// </summary>
        [Description("Binding of exchange '{0}' and queue '{1}' is not found.")]
        BindingNotFound = 126,

        /// <summary>
        /// Cannot subscribe to exchange.
        /// </summary>
        [Description("Cannot subscribe to exchange.")]
        SubscribeToExchange = 127,

        /// <summary>
        /// Exchange cannot route the message.
        /// </summary>
        [Description("Exchange cannot route the message.")]
        ExchangeCannotRouteMessage = 128,

        /// <summary>
        /// Cannot enqueue persistent message in not durable queue.
        /// </summary>
        [Description("Cannot enqueue persistent message in not durable queue '{0}'.")]
        PersistentMessageInNotDurableQueue = 130,

        /// <summary>
        /// Queue length limit is exceeded.
        /// </summary>
        [Description("Queue length limit of '{0}' messages is exceeded.")]
        QueueLengthLimitExceeded = 133,

        /// <summary>
        /// Queue volume limit is exceeded.
        /// </summary>
        [Description("Queue volume limit of '{0}' bytes is exceeded.")]
        QueueVolumeLimitExceeded = 134,

        /// <summary>
        /// You are already logged in. In case of changing user send Logout command first.
        /// </summary>
        [Description("You are already logged in. In case of changing user send Logout command first.")]
        AlreadyLoggedIn = 136,

        /// <summary>
        /// Queue is not started.
        /// </summary>
        [Description("Queue is not started.")]
        QueueNotStarted = 137,

        /// <summary>
        /// Discovering cluster server ClusterKey do not match with the current one.
        /// </summary>
        [Description("Discovering cluster server ClusterKey do not match with the current one.")]
        ClusterKeysDontMatch = 138,

        /// <summary>
        /// You must have cluster administrator rights to perform this operation.
        /// </summary>
        [Description("You must have cluster administrator rights to perform this operation.")]
        AccessDeniedNotClusterAdmin = 141,

        /// <summary>
        /// Request is not replicable.
        /// </summary>
        [Description("Request is not replicable.")]
        NotReplicableRequest = 142,

        /// <summary>
        /// Server not found.
        /// </summary>
        [Description("Server not found.")]
        ServerNotFound = 144,

        /// <summary>
        /// Tracking requests has not been started.
        /// </summary>
        [Description("Request tracking has not been started.")]
        TrackingNotStarted = 145,

        /// <summary>
        /// Tracking requests has been already started.
        /// </summary>
        [Description("Request tracking has been already started.")]
        TrackingAlreadyStarted = 146,

        /// <summary>
        /// User is not started.
        /// </summary>
        [Description("User '{0}' is not started.")]
        UserNotStarted = 147,
    }
}
