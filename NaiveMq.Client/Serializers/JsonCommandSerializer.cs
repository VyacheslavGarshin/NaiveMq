using NaiveMq.Client.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Buffers;
using System.Text;

namespace NaiveMq.Client.Converters
{
    public class JsonCommandSerializer : ICommandSerializer
    {
        private static JsonSerializerSettings _jsonSerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver(),
        };

        public byte[] Serialize(ICommand command)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(command, _jsonSerializerSettings));
        }

        public (byte[] buffer, int length) Serialize(ICommand command, ArrayPool<byte> arrayPool)
        {
            throw new NotImplementedException();
        }
        public ICommand Deserialize(ReadOnlyMemory<byte> bytes, Type type)
        {
            return (ICommand)JsonConvert.DeserializeObject(Encoding.UTF8.GetString(bytes.Span), type, _jsonSerializerSettings);
        }

    }
}
