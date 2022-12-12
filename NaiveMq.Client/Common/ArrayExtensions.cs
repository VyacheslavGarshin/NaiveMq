using System;
using System.Collections.Generic;

namespace NaiveMq.Client.Common
{
    public static class ArrayExtensions
    {
        public static void CopyFrom(this byte[] value, IEnumerable<ReadOnlyMemory<byte>> chunks)
        {
            var index = 0;

            foreach (var chunk in chunks)
            {
                var destination = value.AsMemory(index, chunk.Length);
                chunk.CopyTo(destination);
                index += chunk.Length;
            }
        }
    }
}
