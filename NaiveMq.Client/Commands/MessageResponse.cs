using System;
using System.Runtime.Serialization;
using NaiveMq.Client.AbstractCommands;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Message response.
    /// </summary>
    public class MessageResponse : AbstractResponse<MessageResponse>, IDataCommand
    {
        /// <summary>
        /// Mark as response wich passes the data to the requesting application.
        /// </summary>
        [DataMember(Name = "R")]
        public bool Response { get; set; }

        /// <summary>
        /// Response data.
        /// </summary>
        [IgnoreDataMember]
        public ReadOnlyMemory<byte> Data { get; set; }

        /// <summary>
        /// Creates new MessageResponse command.
        /// </summary>
        public MessageResponse()
        {
        }

        /// <summary>
        /// Creates new MessageResponse command with params.
        /// </summary>
        /// <param name="data"></param>
        public MessageResponse(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        /// <summary>
        /// Create successful respose.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="data"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        public static MessageResponse Ok(IRequest request, ReadOnlyMemory<byte>? data = null, bool response = false)
        {
            return new MessageResponse
            {
                RequestId = request.Id,
                RequestTag = request.Tag,
                Success = true,
                Response = response,
                Data = data ?? new ReadOnlyMemory<byte>(),
            };
        }

        /// <summary>
        /// Create unsuccessful respose.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="errorCode"></param>
        /// <param name="errorMessage"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        public static MessageResponse Error(IRequest request, string errorCode = null, string errorMessage = null, bool response = false)
        {
            return new MessageResponse
            {
                RequestId = request.Id,
                RequestTag = request.Tag,
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                Response = response,
            };
        }

        /// <summary>
        /// Copy message response.
        /// </summary>
        /// <returns></returns>
        public override MessageResponse Copy()
        {
            var result = base.Copy();

            result.Response = Response;
            result.Data = Data;

            return result;
        }

        /// <inheritdoc/>
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
