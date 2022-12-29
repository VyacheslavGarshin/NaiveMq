using NaiveMq.Client.Commands;
using System;
using System.Buffers;

namespace NaiveMq.Client.Converters
{
    public interface ICommandSerializer
    {
        byte[] Serialize(ICommand command);

        (byte[] buffer, int length) Serialize(ICommand command, ArrayPool<byte> arrayPool);

        ICommand Deserialize(ReadOnlyMemory<byte> bytes, Type type);
    }
}
