using NaiveMq.Client.Commands;
using System;

namespace NaiveMq.Client.Converters
{
    public interface ICommandSerializer
    {
        byte[] Serialize(ICommand command);

        ICommand Deserialize(ReadOnlyMemory<byte> bytes, Type type);
    }
}
