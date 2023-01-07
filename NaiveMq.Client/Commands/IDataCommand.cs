using System;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Interface for data commands.
    /// </summary>
    /// <remarks>
    /// Data will not be serialized as a part of the command but will be sent separatelly increasing the performance.
    /// Don't forget to mark <see cref="Data"/> field as ignored in serialization with IgnoreDataMember attribute.
    /// </remarks>
    public interface IDataCommand : ICommand
    {
        /// <summary>
        /// Data part of the command.
        /// </summary>
        public ReadOnlyMemory<byte> Data { get; set; }
    }
}
