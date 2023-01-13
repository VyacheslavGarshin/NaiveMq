using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Absract implementation for any delete request.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public abstract class AbstractDeleteRequest<TResponse> : AbstractRequest<TResponse>
        where TResponse : IResponse
    {
        /// <summary>
        /// Do not throw exception if object does not exists.
        /// </summary>
        /// <remarks>Default is false.</remarks>
        [DataMember(Name = "Tr")]
        public bool Try { get; set; } = false;

        /// <summary>
        /// Creates AbstractDeleteRequest.
        /// </summary>
        protected AbstractDeleteRequest()
        {
        }

        /// <summary>
        /// Creates AbstractDeleteRequest with params.
        /// </summary>
        /// <param name="try"></param>
        public AbstractDeleteRequest(bool @try)
        {
            Try = @try;
        }
    }
}
