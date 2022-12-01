using System;

namespace NaiveMq.Client.Commands
{
    public interface ICommand
    {
        /// <summary>
        /// Id of the command. Will be set by client on send if empty.
        /// </summary>
        public Guid Id { get; set; }
    }
}
