using NaiveMq.Client.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace NaiveMq.Client.Converters
{
    public class JsonCommandConverter : ICommandConverter
    {
        private static StringEnumConverter _stringEnumConverter = new ();
        
        private static JsonSerializerSettings _jsonSerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter> { _stringEnumConverter },
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
