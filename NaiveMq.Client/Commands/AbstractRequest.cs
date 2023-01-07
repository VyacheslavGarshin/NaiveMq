using System;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Abstract request.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public abstract class AbstractRequest<TResponse> : AbstractCommand, IRequest<TResponse>
        where TResponse : IResponse
    {
        /// <inheritdoc/>
        [DataMember(Name = "T")]
        public string Tag { get; set; }

        /// <inheritdoc/>
        [DataMember(Name = "C")]
        public bool Confirm { get; set; } = true;

        /// <inheritdoc/>
        [DataMember(Name = "CT")]
        public TimeSpan? ConfirmTimeout { get; set; }

        /// <inheritdoc/>
        public override void Validate()
        {
            if (Confirm)
            {
                if (ConfirmTimeout == null)
                {
                    throw new ClientException(ErrorCode.ParameterNotSet, new object[] { nameof(ConfirmTimeout) });
                }

                if (ConfirmTimeout.Value <= TimeSpan.Zero)
                {
                    throw new ClientException(ErrorCode.ParameterLessThan, new object[] { nameof(ConfirmTimeout), 0 });
                }
            }
        }
    }
}
