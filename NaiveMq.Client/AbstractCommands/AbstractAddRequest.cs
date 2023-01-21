using System.Runtime.Serialization;
using NaiveMq.Client.Commands;

namespace NaiveMq.Client.AbstractCommands
{
    /// <summary>
    /// Absract implementation for any add request.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public abstract class AbstractAddRequest<TResponse> : AbstractRequest<TResponse>
        where TResponse : IResponse
    {
        /// <summary>
        /// Do not throw exception if object already exists.
        /// </summary>
        /// <remarks>Default is false.</remarks>
        [DataMember(Name = "Tr")]
        public bool Try { get; set; } = false;

        /// <summary>
        /// Creates AbstractAddRequest.
        /// </summary>
        protected AbstractAddRequest()
        {
        }

        /// <summary>
        /// Creates AbstractAddRequest with params.
        /// </summary>
        /// <param name="try"></param>
        public AbstractAddRequest(bool @try)
        {
            Try = @try;
        }
    }
}
