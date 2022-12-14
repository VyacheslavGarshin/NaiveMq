using System;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractRequest<TResponse> : AbstractCommand, IRequest<TResponse>
        where TResponse : IResponse
    {
        public bool Confirm { get; set; } = true;

        public TimeSpan? ConfirmTimeout { get; set; }

        public override void Validate()
        {
            if (Confirm && (ConfirmTimeout == null || ConfirmTimeout.Value <= TimeSpan.Zero))
            {
                throw new ClientException(ErrorCode.ConfirmTimeoutNotSet);
            }
        }
    }
}
