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

        public async override Task PrepareAsync(CancellationToken cancellationToken)
        {
            await base.PrepareAsync(cancellationToken);

            if (Responses != null && Responses.Count > 0)
            {
                foreach (var request in Responses)
                {
                    await request.PrepareAsync(cancellationToken);
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

        public override async Task RestoreAsync(CancellationToken cancellationToken)
        {
            await base.RestoreAsync(cancellationToken);

            using var stream = Data.AsStream();

            Responses = (await new CommandPacker(new JsonCommandConverter()).Unpack(stream, cancellationToken)).
               Cast<IResponse>().ToList();

            Data = new ReadOnlyMemory<byte>();
        }
    }
}
