using System;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractRequest<TResponse> : AbstractCommand, IRequest<TResponse>
        where TResponse : IResponse
    {
        public string Tag { get; set; }

        public bool Confirm { get; set; } = true;

        public TimeSpan? ConfirmTimeout { get; set; }

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
