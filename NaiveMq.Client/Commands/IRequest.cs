using System;

namespace NaiveMq.Client.Commands
{
    public interface IRequest : ICommand
    {
        /// <summary>
        /// Confirmation required.
        /// </summary>
        /// <remarks>In this case a IResponse command should be send back to requesting side. Otherwise sender should revert operation and receiver rise an error after timeout.</remarks>
        bool Confirm { get; set; }

        /// <summary>
        /// Optional confirmation timeout.
        /// </summary>
        /// <remarks>If not set then <see cref="NaiveMqClient.Timeout"/> is used.</remarks>
        TimeSpan? ConfirmTimeout { get; }
    }

    public interface IRequest<TResponse> : IRequest
        where TResponse : IResponse
    {
    }
}
