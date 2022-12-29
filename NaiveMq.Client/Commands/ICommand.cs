using NaiveMq.Client.Common;
using System;

namespace NaiveMq.Client.Commands
{
    public interface ICommand
    {
        /// <summary>
        /// Id of the command. Will be set by client on send if empty.
        /// </summary>
        public Guid Id { get; set; }

        public void Prepare(CommandPacker commandPacker);

        public void Validate();

        public void Restore(CommandPacker commandPacker);
    }
}
