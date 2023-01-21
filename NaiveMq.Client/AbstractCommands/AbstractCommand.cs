using NaiveMq.Client.Cogs;
using NaiveMq.Client.Commands;
using System;
using System.Runtime.Serialization;

namespace NaiveMq.Client.AbstractCommands
{
    /// <summary>
    /// Abstract ICommand implementation.
    /// </summary>
    [DataContract]
    public abstract class AbstractCommand : ICommand
    {
        /// <inheritdoc/>
        [DataMember(Name = "I")]
        public Guid Id { get; set; }

        /// <inheritdoc/>
        public virtual void Prepare(CommandPacker commandPacker)
        {
            if (Id == Guid.Empty)
            {
                Id = Guid.NewGuid();
            }
        }

        /// <inheritdoc/>
        public virtual void Restore(CommandPacker commandPacker)
        {
        }

        /// <inheritdoc/>
        public virtual void Validate()
        {
            if (Id == Guid.Empty)
            {
                throw new ClientException(ErrorCode.EmptyCommandId);
            }
        }
    }
}
