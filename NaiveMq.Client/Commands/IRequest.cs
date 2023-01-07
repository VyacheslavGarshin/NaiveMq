using System;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Request interface.
    /// </summary>
    public interface IRequest : ICommand
    {
        /// <summary>
        /// Custom optional tag to identify request by client application.
        /// </summary>
        string Tag { get; set; }

        /// <summary>
        /// Confirmation required from the other side.
        /// </summary>
        /// <remarks>Default is true. In this case a IResponse command should be send back to requesting side. Otherwise sender should revert operation and receiver rise an error after timeout.</remarks>
        bool Confirm { get; set; }

        /// <summary>
        /// Optional confirmation timeout.
        /// </summary>
        /// <remarks>If not set then <see cref="NaiveMqClientOptions.ConfirmTimeout"/> is used.</remarks>
        TimeSpan? ConfirmTimeout { get; set; }
    }

    /// <summary>
    /// Generic request interface defining response type for this command.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public interface IRequest<TResponse> : IRequest
        where TResponse : IResponse
    {
    }
}
