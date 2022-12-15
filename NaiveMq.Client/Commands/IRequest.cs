using System;

namespace NaiveMq.Client.Commands
{
    public interface IRequest : ICommand
    {
        /// <summary>
        /// Custom optional tag to identify request.
        /// </summary>
        string Tag { get; set; }

        /// <summary>
        /// Confirmation required.
        /// </summary>
        /// <remarks>Default is true. In this case a IResponse command should be send back to requesting side. Otherwise sender should revert operation and receiver rise an error after timeout.</remarks>
        bool Confirm { get; set; }

        /// <summary>
        /// Optional confirmation timeout.
        /// </summary>
        /// <remarks>If not set then <see cref="NaiveMqClient.ConfirmTimeout"/> is used.</remarks>
        TimeSpan? ConfirmTimeout { get; set; }
    }

    public interface IRequest<TResponse> : IRequest
        where TResponse : IResponse
    {
    }
}
