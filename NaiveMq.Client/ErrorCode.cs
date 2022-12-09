using System.ComponentModel;

namespace NaiveMq.Client
{
    public enum ErrorCode
    {
        [Description("Command '{0}' not found.")]
        CommandNotFound = 1,

        [Description("Command Id must be set.")]
        EmptyCommandId = 2,

        [Description("Client is stopped.")]
        ClientStopped = 3,

        [Description("Confirmation timeout.")]
        ConfirmationTimeout = 4,

        [Description("Confirmation error.")]
        ConfirmationError = 5,

        [Description("Connection parameters are empty.")]
        ConnectionParametersAreEmpty = 6,

        [Description("Command length cannot exceed 1000. Current length id '{0}'.")]
        CommandNameLengthLong = 7,

        [Description("Cannot find handler for command '{0}'.")]
        CommandHandlerNotFound = 101,

        [Description("Unexpected error executing command. Error: {0}.")]
        UnexpectedCommandHandlerExecutionError = 102,

        [Description("Subscription on queue '{0}' already exists.")]
        SubscriptionAlreadyExists = 103,

        [Description("Subscription on queue '{0}' is not found.")]
        SubscriptionNotFound = 104,

        [Description("Queue '{0}' already exists.")]
        QueueAlreadyExists = 105,

        [Description("Queue '{0}' is not found.")]
        QueueNotFound = 106,

        [Description("Queue '{0}' is empty.")]
        QueueIsEmpty = 107,

        [Description("Cannot perform operation. Persistence storage is not set up.")]
        PersistenceStorageNotSetUp = 108,

        [Description("User '{0}' already exists.")]
        UserAlreadyExists = 109,

        [Description("You must have admin rights to perform this operation.")]
        AccessDeniedNotAdmin = 110,

        [Description("User '{0}' queues are not found.")]
        UserQueuesNotFound = 111,

        [Description("User is not authenticated.")]
        UserNotAuthenticated = 112,

        [Description("Username or password is not correct.")]
        UserOrPasswordNotCorrect = 113,

        [Description("User '{0}' is not found.")]
        UserNotFound = 114,

        [Description("Users cannot delete themselves.")]
        UserDeleteSelf = 115,

        [Description("Users cannot unset administrator privilege on themselves.")]
        UserUnsetAdministratorSelf = 116,

        [Description("Wrong password.")]
        WrongPassword = 117,

        [Description("New password cannot be the same as an old one.")]
        NewPasswordTheSame = 118,

        [Description("Password сannot be empty.")]
        PasswordEmpty = 119,

        [Description("User '{0}' bindings are not found.")]
        UserBindingsNotFound = 120,

        [Description("Exchange '{0}' is already bound to queue '{1}'.")]
        ExchangeAlreadyBoundToQueue = 121,

        [Description("Exchange '{0}' is not found.")]
        ExchangeNotFound = 122,

        [Description("Durable binding must bind both durable exchange and queue.")]
        DurableBindingCheck = 123,

        [Description("Cannot bind exchange.")]
        BindExchange = 124,

        [Description("Cannot bind to a queue which is not exchange.")]
        BindToQueue = 125,

        [Description("Binding of exchange '{0}' and queue '{1}' is not found.")]
        BindingNotFound = 126,

        [Description("Cannot subscribe to exchange.")]
        SubscribeToExchange = 127,

        [Description("Exchange cannot route the message.")]
        ExchangeCannotRouteMessage = 128,

        [Description("Request message '{0}' already registered.")]
        RequestAlreadyRegistered = 129,

        [Description("Cannot enqueue persistent message in not durable queue '{0}'.")]
        PersistentMessageInNotDurableQueue = 130,

        [Description("Queue limit cannot be less than one. Leave it empty for limitless queue.")]
        QueueLimitLessThanOne = 131,

        [Description("Request cannot be persistent.")]
        PersistentRequest = 132,

        [Description("Queue length limit of '{0}' messages is exceeded.")]
        QueueLengthLimitExceeded = 133,

        [Description("Queue volume limit of '{0}' bytes is exceeded.")]
        QueueVolumeLimitExceeded = 134,

        [Description("Request confirm timeout must be set when confirmation is requested.")]
        ConfirmTimeoutNotSet = 135,
    }
}
