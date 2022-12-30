using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Buffers;
using System.Text;

namespace NaiveMq.Client.Serializers
{
    public class JsonCommandSerializer : ICommandSerializer
    {
        private static JsonSerializerSettings _jsonSerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver(),
        };

        public byte[] Serialize(object obj)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, _jsonSerializerSettings));
        }

        public (byte[] buffer, int length) Serialize(object obj, ArrayPool<byte> arrayPool)
        {
            throw new NotImplementedException();
        }

        public object Deserialize(ReadOnlyMemory<byte> bytes, Type type)
        {
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(bytes.Span), type, _jsonSerializerSettings);
        }

    }
}
