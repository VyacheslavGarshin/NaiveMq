using NaiveMq.Client.Common;
using System;
using System.Buffers;

namespace NaiveMq.Client.Serializers
{
    public interface ICommandSerializer
    {
        byte[] Serialize(object obj);

        PackResult Serialize(object obj, ArrayPool<byte> arrayPool);

        object Deserialize(ReadOnlyMemory<byte> bytes, Type type);
    }
}
