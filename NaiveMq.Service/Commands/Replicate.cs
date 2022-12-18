using NaiveMq.Client.Commands;
using Newtonsoft.Json;

namespace NaiveMq.Service.Commands
{
    public class Replicate : AbstractRequest<Confirmation>, IDataCommand
    {
        public string User { get; set; }

        public string CommandType { get; set; }

        [JsonIgnore]
        public IRequest Command { get; set; }

        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public Replicate()
        {
        }

        public Replicate(string commandType, ReadOnlyMemory<byte> data)
        {
            CommandType = commandType;
            Data = data;
        }
    }
}