using NaiveMq.Client.Common;
using System;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public abstract class AbstractCommand : ICommand
    {
        [DataMember(Name = "I")]
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
