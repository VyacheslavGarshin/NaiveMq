using Newtonsoft.Json;
using System;

namespace NaiveMq.Client.Commands
{
    public class Confirmation : AbstractResponse<Confirmation>, IDataCommand
    {
        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public static Confirmation Ok(Guid requestId, ReadOnlyMemory<byte> data)
        {
            return new Confirmation
            {
                RequestId = requestId,
                Success = true,
                Data = data,
            };
        }
    }
}
