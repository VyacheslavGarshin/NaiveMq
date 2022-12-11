using Newtonsoft.Json;
using System;

namespace NaiveMq.Client.Commands
{
    public class Confirmation : AbstractResponse<Confirmation>, IDataCommand
    {
        [JsonIgnore]
        public byte[] Data { get; set; }

        public static Confirmation Ok(Guid requestId, byte[] data = null)
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
