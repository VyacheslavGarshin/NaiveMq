using Newtonsoft.Json;
using System;

namespace NaiveMq.Client.Commands
{
    public class Confirmation : AbstractResponse<Confirmation>, IDataCommand
    {
        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public static Confirmation Ok(IRequest request, ReadOnlyMemory<byte> data)
        {
            return new Confirmation
            {
                RequestId = request.Id,
                RequestTag = request.Tag,
                Success = true,
                Data = data,
            };
        }
    }
}
