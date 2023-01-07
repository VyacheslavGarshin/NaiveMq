using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Abstract get entity response.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class AbstractGetResponse<T> : AbstractResponse<AbstractGetResponse<T>>
    {
        /// <summary>
        /// Entity.
        /// </summary>
        [DataMember(Name = "E")]
        public T Entity { get; set; }
    }
}
