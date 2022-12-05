using NaiveMq.Client.Commands;
using Newtonsoft.Json;
using System;
using System.Text;

namespace NaiveMq.Client.Converters
{
    public class JsonCommandConverter : ICommandConverter
    {
        public ICommand Deserialize(byte[] bytes, Type type)
        {
            return (ICommand)JsonConvert.DeserializeObject(Encoding.UTF8.GetString(bytes), type);
        }

        public byte[] Serialize(ICommand command)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(command));
        }
    }
}
