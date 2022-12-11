using System;

namespace NaiveMq.Client.Commands
{
    public interface IDataCommand : ICommand
    {
        public ReadOnlyMemory<byte> Data { get; set; }
    }
}
