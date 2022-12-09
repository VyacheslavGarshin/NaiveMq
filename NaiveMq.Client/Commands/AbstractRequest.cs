﻿using NaiveMq.Client.Enums;
using System;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractRequest<TResponse> : IRequest<TResponse>
        where TResponse : IResponse
    {
        public Guid Id { get; set; }

        public bool Confirm { get; set; } = true;

        public TimeSpan? ConfirmTimeout { get; set; }

        public virtual void Validate()
        {
            if (Confirm && (ConfirmTimeout == null || ConfirmTimeout.Value <= TimeSpan.Zero))
            {
                throw new ClientException(ErrorCode.ConfirmTimeoutNotSet);
            }
        }
    }
}
