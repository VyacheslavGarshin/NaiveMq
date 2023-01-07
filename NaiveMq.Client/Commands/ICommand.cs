using NaiveMq.Client.Cogs;
using System;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Initial interface for the command.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Id of the command. Will be set by client on send if empty.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Prepare command before sending.
        /// </summary>
        /// <param name="commandPacker"></param>
        /// <remarks>Called before <see cref="Validate"/>.</remarks>
        public void Prepare(CommandPacker commandPacker);

        /// <summary>
        /// Restore command received from the channel.
        /// </summary>
        /// <param name="commandPacker"></param>
        /// <remarks>Called before <see cref="Validate"/>.</remarks>
        public void Restore(CommandPacker commandPacker);

        /// <summary>
        /// Validate command before sending and after receiving.
        /// </summary>
        public void Validate();
    }
}
