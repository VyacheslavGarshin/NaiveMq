using NaiveMq.Client.Cogs;
using System;
using System.Buffers;

namespace NaiveMq.Client.Serializers
{
    /// <summary>
    /// Command serializer interface.
    /// </summary>
    public interface ICommandSerializer
    {
        /// <summary>
        /// Serialize.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        byte[] Serialize(object obj);

        /// <summary>
        /// Serialize to buffer.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="arrayPool"></param>
        /// <returns></returns>
        BufferResult Serialize(object obj, ArrayPool<byte> arrayPool);

        /// <summary>
        /// Deserialize.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        object Deserialize(ReadOnlyMemory<byte> bytes, Type type);
    }
}
