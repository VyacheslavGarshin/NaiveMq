using System.ComponentModel;

namespace NaiveMq.Client
{
    public enum ErrorCode
    {
        [Description("Message format is wrong. Must be 'Command|{ Id = ... }'.")]
        WrongMessageFormat = 1,

        [Description("Command '{0}' not found.")]
        CommandNotFound = 2,

        [Description("Cannot deserialize command. Should be in JSON format. Error: {0}")]
        WrongCommandFormat = 3,

        [Description("Command Id must be set.")]
        EmptyCommandId = 4,

        [Description("Error parsing request id. Must be empty of guid.")]
        WrongRequestId = 5,

        [Description("Unexpected error parsing message. Error: {0}")]
        UnexpectedErrorDuringMessageParsing = 6,

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
        UserCannotDeleteSelf = 115,

        [Description("Users cannot unset administrator privilege on themselves.")]
        UserCannotUnsetAdministratorSelf = 116,

        [Description("Wrong password.")]
        WrongPassword = 117,

        [Description("New password cannot be the same as an old one.")]
        NewPasswordCannotBeTheSame = 118,

        [Description("Password сannot be empty.")]
        PasswordCannotBeEmpty = 119,

        [Description("User '{0}' bindings are not found.")]
        UserBindingsNotFound = 120,

        [Description("Exchange '{0}' is already bound to queue '{1}'.")]
        ExchangeAlreadyBoundToQueue = 121,

        [Description("Exchange '{0}' is not found.")]
        ExchangeNotFound = 122,

        [Description("Durable binding must bind both durable exchange and queue.")]
        DurableBindingCheck = 123,

        [Description("Cannot bind exchange.")]
        CannotBindExchange = 124,

        [Description("Cannot bind to a queue which is not exchange.")]
        CannotBindToQueue = 125,

        [Description("Binding of exchange '{0}' and queue '{1}' is not found.")]
        BindingNotFound = 126,

        [Description("Cannot subscribe to exchange.")]
        CannotSubscribeToExchange = 127,
    }
}
