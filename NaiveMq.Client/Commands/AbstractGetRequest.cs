using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Absract implementation for any get entity request.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public abstract class AbstractGetRequest<TResponse> : AbstractRequest<TResponse>
        where TResponse : IResponse
    {
        /// <summary>
        /// Try to get object.
        /// </summary>
        /// <remarks>Returns null if object is not found. Overwise raises an exception. Default is true.</remarks>
        [DataMember(Name = "Tr")]
        public bool Try { get; set; } = true;

        /// <summary>
        /// Creates AbstractGetRequest.
        /// </summary>
        protected AbstractGetRequest()
        {
        }

        /// <summary>
        /// Creates AbstractGetRequest with params.
        /// </summary>
        /// <param name="try"></param>
        public AbstractGetRequest(bool @try)
        {
            Try = @try;
        }
    }
}
