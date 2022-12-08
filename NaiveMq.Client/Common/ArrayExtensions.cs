using System.Collections.Generic;

namespace NaiveMq.Client.Common
{
    public static class ArrayExtensions
    {
        public static void CopyFrom(this byte[] value, IEnumerable<byte[]> chunks)
        {
            var index = 0;

            foreach (var chunk in chunks)
            {
                chunk.CopyTo(value, index);
                index += chunk.Length;
            }
        }
    }
}
