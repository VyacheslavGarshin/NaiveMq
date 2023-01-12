using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
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
        [DataMember(Name = "Tr")]
        public bool Try { get; set; } = false;
    }
}
