using System;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Response interface.
    /// </summary>
    public interface IResponse : ICommand
    {
        /// <summary>
        /// <see cref="ICommand.Id"/> of the original request.
        /// </summary>
        public Guid RequestId { get; set; }

        /// <summary>
        /// <see cref="IRequest.Tag"/> of the original request.
        /// </summary>
        public string RequestTag { get; set; }

        /// <summary>
        /// Successfullness of the operation.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error code is string representation. Often <see cref="Client.ErrorCode"/>.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Error message.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
