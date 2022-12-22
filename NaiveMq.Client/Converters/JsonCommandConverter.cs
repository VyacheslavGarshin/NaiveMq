using NaiveMq.Client.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Text;

namespace NaiveMq.Client.Converters
{
    public class JsonCommandConverter : ICommandConverter
    {
        private static JsonSerializerSettings _jsonSerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver(),
        };

        public ICommand Deserialize(ReadOnlyMemory<byte> bytes, Type type)
        {
            return (ICommand)JsonConvert.DeserializeObject(Encoding.UTF8.GetString(bytes.Span), type, _jsonSerializerSettings);
        }

        public byte[] Serialize(ICommand command)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(command, _jsonSerializerSettings));
        }
    }
}
