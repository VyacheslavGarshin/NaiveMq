using System;

namespace NaiveMq.Client.Commands
{
    public interface IDataCommand : ICommand
    {
        public byte[] Data { get; set; }
    }
}
