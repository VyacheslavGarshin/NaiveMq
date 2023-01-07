using Naive.Serializer;
using NaiveMq.Client.Cogs;
using System;
using System.Buffers;
using System.IO;

namespace NaiveMq.Client.Serializers
{
    /// <summary>
    /// Naive command serializer.
    /// </summary>
    public class NaiveCommandSerializer : ICommandSerializer
    {
        /// <inheritdoc/>
        public byte[] Serialize(object obj)
        {
            return NaiveSerializer.Serialize(obj, new NaiveSerializerOptions { IgnoreNullValue = true });          
        }

        /// <inheritdoc/>
        public BufferResult Serialize(object obj, ArrayPool<byte> arrayPool)
        {
            using (var stream = new MemoryStream())
            {
                NaiveSerializer.Serialize(obj, stream, new NaiveSerializerOptions { IgnoreNullValue = true });
                var buffer = arrayPool.Rent((int)stream.Length);
                stream.Position = 0;
                stream.Read(buffer, 0, (int)stream.Length);
                return new BufferResult(buffer, (int)stream.Length);
            }
        }

        /// <inheritdoc/>
        public object Deserialize(ReadOnlyMemory<byte> bytes, Type type)
        {
            // ReadOnlyMemory stream is surprizingly slow
            return NaiveSerializer.Deserialize(bytes.ToArray(), type);
        }
    }
}
