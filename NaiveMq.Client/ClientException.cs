using NaiveMq.Client.Cogs;
using NaiveMq.Client.Commands;
using System;

namespace NaiveMq.Client
{
    /// <summary>
    /// Client exception.
    /// </summary>
    public class ClientException : Exception
    {
        /// <summary>
        /// Error code.
        /// </summary>
        public ErrorCode ErrorCode { get; set; }

        /// <summary>
        /// Server response in case of error on sending request.
        /// </summary>
        public IResponse Response { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="errorCode"></param>
        public ClientException(ErrorCode errorCode) : this(errorCode, errorCode.GetDescription(), null)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="message"></param>
        public ClientException(ErrorCode errorCode, string message) : this(errorCode, message, null)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="args"></param>
        public ClientException(ErrorCode errorCode, object[] args) : this(errorCode, string.Format(errorCode.GetDescription(), args), null)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="innerException"></param>
        public ClientException(ErrorCode errorCode, Exception innerException) : this(errorCode, errorCode.GetDescription(), innerException)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public ClientException(ErrorCode errorCode, string message, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}
