using NaiveMq.Client.Common;
using System;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractCommand : ICommand
    {
        public Guid Id { get; set; }

        public virtual void Prepare(CommandPacker commandPacker)
        {
            if (Id == Guid.Empty)
            {
                Id = Guid.NewGuid();
            }
        }

        public virtual void Validate()
        {
            if (Id == Guid.Empty)
            {
                throw new ClientException(ErrorCode.EmptyCommandId);
            }
        }

        public virtual void Restore(CommandPacker commandPacker)
        {
        }
    }
}
