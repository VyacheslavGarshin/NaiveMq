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

        [Description("Cannot create durable queue. Persistence storage is not set.")]
        CannotCreateDurableQueue = 108,

        [Description("User '{0}' already exists.")]
        UserAlreadyExists = 109,

        [Description("You don't have access to add a new user.")]
        AccessDeniedAddingUser = 110,

        [Description("User '{0}' queues are not found.")]
        UserQueuesNotFound = 111,

        [Description("User is not authenticated.")]
        UserNotAuthenticated = 112,

        [Description("Username or password is not correct.")]
        UserOrPasswordNotCorrect = 113,
    }
}
