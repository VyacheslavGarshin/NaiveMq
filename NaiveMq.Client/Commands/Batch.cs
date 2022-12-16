using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NaiveMq.Client.Commands
{
    public class Batch : AbstractRequest<BatchResponse>, IDataCommand
    {
        public List<Message> Messages { get; set; }

        /// <summary>
        /// Combined data of all requests. Automatically generated from Requests on sending command.
        /// </summary>
        /// <remarks>When receive message data is available only during handling in event.</remarks>
        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public override async Task PrepareAsync(CancellationToken cancellationToken)
        {
            await base.PrepareAsync(cancellationToken);

            if (Messages != null && Messages.Count > 0)
            {
                var dataLength = 0;

                foreach (var message in Messages)
                {
                    await message.PrepareAsync(cancellationToken);

                    message.Confirm = Confirm;
                    message.ConfirmTimeout = ConfirmTimeout;

                    dataLength += message.Data.Length;
                }

                var data = new byte[dataLength + Messages.Count * 4];
                using var memoryStream = new MemoryStream(data);

                foreach (var message in Messages)
                {
                    if (message is IDataCommand dataCommand)
                    {
                        await memoryStream.WriteAsync(BitConverter.GetBytes(dataCommand.Data.Length));
                        await memoryStream.WriteAsync(dataCommand.Data);
                    }
                    else
                    {
                        await memoryStream.WriteAsync(BitConverter.GetBytes(0));
                    }
                }

                Data = data;
            }
        }

        public override void Validate()
        {
            base.Validate();

            if (Messages == null || Messages.Count == 0)
            {
                throw new ClientException(ErrorCode.BatchMessagesEmpty);
            }

            foreach (var message in Messages)
            {
                message.Validate();

                if (message.Request)
                {
                    throw new ClientException(ErrorCode.BatchContainsRequestMessage);
                }
            }
        }

        public override async Task RestoreAsync(CancellationToken cancellationToken)
        {
            await base.RestoreAsync(cancellationToken);

            // todo get rid of ToArray
            using var memoryStream = new MemoryStream(Data.ToArray());

            foreach (var message in Messages)
            {
                var lengthBytes = new byte[4];
                // todo pass ct
                await memoryStream.ReadAsync(lengthBytes, 0, 4, CancellationToken.None);
                var length = BitConverter.ToInt32(lengthBytes, 0);

                // todo use arraypool
                var bytes = new byte[length];
                await memoryStream.ReadAsync(bytes, 0, length, CancellationToken.None);

                message.Data = bytes;
            }
        }
    }
}
