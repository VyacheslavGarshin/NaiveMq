using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Commands
{
    public class MessageResponse : AbstractResponse<MessageResponse>, IDataCommand
    {
        public bool Response { get; set; }

        [JsonIgnore]
        [IgnoreDataMember]
        public ReadOnlyMemory<byte> Data { get; set; }

        public MessageResponse()
        {
        }

        public MessageResponse(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public static MessageResponse Ok(IRequest request, ReadOnlyMemory<byte> data, bool response = false)
        {
            return new MessageResponse
            {
                RequestId = request.Id,
                RequestTag = request.Tag,
                Success = true,
                Response = response,
                Data = data,
            };
        }

        public override MessageResponse Copy()
        {
            var result = base.Copy();

            result.Response = Response;
            result.Data = Data;

            return result;
        }

        public override void Validate()
        {
            base.Validate();

            if (Response && Data.Length == 0)
            {
                throw new ClientException(Client.ErrorCode.DataIsEmpty);
            }
        }
    }
}
