using CommunityToolkit.HighPerformance;
using NaiveMq.Client.Common;
using NaiveMq.Client.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaiveMq.Client.Commands
{
    public class BatchResponse : AbstractResponse<BatchResponse>, IDataCommand
    {
        [JsonIgnore]
        public List<IResponse> Responses { get; set; }

        /// <summary>
        /// Combined packed responses. Automatically generated from Responses on sending command.
        /// </summary>
        /// <remarks>When receive Data is reconstructed back to Responses. Then cleared.</remarks>
        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public BatchResponse()
        {
        }

        public BatchResponse(List<IResponse> responses)
        {
            Responses = responses;
        }

        public override void Prepare()
        {
            base.Prepare();

            if (Responses != null && Responses.Count > 0)
            {
                foreach (var request in Responses)
                {
                    request.Prepare();
                }

                Data = new CommandPacker(new JsonCommandConverter()).Pack(Responses);
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

        public override void Restore()
        {
            base.Restore();

            using var stream = Data.AsStream();

            var task = new CommandPacker(new JsonCommandConverter()).Unpack(stream, CancellationToken.None);
            task.Wait();

            Responses = task.Result.Cast<IResponse>().ToList();

            Data = new ReadOnlyMemory<byte>();
        }
    }
}
