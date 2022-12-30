using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractGetRequest<TResponse> : AbstractRequest<TResponse>
        where TResponse : IResponse
    {
        /// <summary>
        /// Try to get entity.
        /// </summary>
        /// <remarks>Return null if entity is not found. Overwise raise an exception. True by default.</remarks>
        [DataMember(Name = "Tr")]
        public bool Try { get; set; } = true;
    }
}
