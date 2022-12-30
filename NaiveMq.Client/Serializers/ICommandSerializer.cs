using System;
using System.Buffers;

namespace NaiveMq.Client.Serializers
{
    public interface ICommandSerializer
    {
        byte[] Serialize(object obj);

        (byte[] buffer, int length) Serialize(object obj, ArrayPool<byte> arrayPool);

        object Deserialize(ReadOnlyMemory<byte> bytes, Type type);
    }
}
