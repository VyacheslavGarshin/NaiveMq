using Naive.Serializer;
using Naive.Serializer.Cogs;
using NaiveMq.Client.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace NaiveMq.Client.Commands
{
    public class BatchResponse : AbstractResponse<BatchResponse>, IDataCommand
    {
        [JsonIgnore]
        [IgnoreDataMember]
        public List<IResponse> Responses { get; set; }

        /// <summary>
        /// Combined packed responses. Automatically generated from Responses on sending command.
        /// </summary>
        /// <remarks>When receive Data is reconstructed back to Responses. Then cleared.</remarks>
        [JsonIgnore]
        [IgnoreDataMember]
        public ReadOnlyMemory<byte> Data { get; set; }

        public BatchResponse()
        {
        }

        public BatchResponse(List<IResponse> responses)
        {
            Responses = responses;
        }

        public override void Prepare(CommandPacker commandPacker)
        {
            base.Prepare(commandPacker);

            if (Responses != null && Responses.Count > 0)
            {
                foreach (var request in Responses)
                {
                    request.Prepare(commandPacker);
                }

                Data = commandPacker.Pack(Responses);
            }
        }

        public override void Validate()
        {
            base.Validate();

            if (Responses == null || Responses.Count == 0)
            {
                throw new ClientException(Client.ErrorCode.BatchCommandsEmpty);
            }

            foreach (var response in Responses)
            {
                response.Validate();
            }
        }

        public override void Restore(CommandPacker commandPacker)
        {
            base.Restore(commandPacker);

            using var stream = new RomStream(Data);

            var task = commandPacker.ReadAsync(stream, CancellationToken.None);
            task.Wait();

            Responses = task.Result.Cast<IResponse>().ToList();

            Data = new ReadOnlyMemory<byte>();
        }
    }
}
