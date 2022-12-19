using NaiveMq.Client.Common;
using NaiveMq.Client.Converters;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaiveMq.Client.Commands
{
    // todo redo on any command by making Messages IRequest and JsonIgnore
    public class Batch : AbstractRequest<BatchResponse>, IDataCommand
    {
        [JsonIgnore]
        public List<IRequest> Requests { get; set; }

        /// <summary>
        /// Combined packed requests. Automatically generated from Requests on sending command.
        /// </summary>
        /// <remarks>When receive message data is available only during handling in event.</remarks>
        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public Batch()
        {
        }

        public Batch(List<IRequest> requests)
        {
            Requests = requests;
        }

        public override async Task PrepareAsync(CancellationToken cancellationToken)
        {
            await base.PrepareAsync(cancellationToken);

            if (Requests != null && Requests.Count > 0)
            {
                foreach (var request in Requests)
                {
                    await request.PrepareAsync(cancellationToken);
                }

                Data = new CommandPacker(new JsonCommandConverter()).Pack(Requests);
            }
        }

        public override void Validate()
        {
            base.Validate();

            if (Requests == null || Requests.Count == 0)
            {
                throw new ClientException(ErrorCode.BatchCommandsEmpty);
            }

            foreach (var request in Requests)
            {
                request.Validate();

                if (request is Message message && message.Request)
                {
                    throw new ClientException(ErrorCode.BatchContainsRequestMessage);
                }
            }
        }

        public override async Task RestoreAsync(CancellationToken cancellationToken)
        {
            await base.RestoreAsync(cancellationToken);

            Requests = (await new CommandPacker(new JsonCommandConverter()).Unpack(Data.ToArray(), cancellationToken)).
                Cast<IRequest>().ToList();

            Data = new ReadOnlyMemory<byte>();
        }
    }
}
