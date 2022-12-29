using CommunityToolkit.HighPerformance;
using NaiveMq.Client.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace NaiveMq.Client.Commands
{
    public class Batch : AbstractRequest<BatchResponse>, IDataCommand
    {
        [JsonIgnore]
        [IgnoreDataMember]
        public List<IRequest> Requests { get; set; }

        /// <summary>
        /// Combined packed requests. Automatically generated from Requests on sending command.
        /// </summary>
        /// <remarks>When receive Data is reconstructed back to Requests. Then cleared.</remarks>
        [JsonIgnore]
        [IgnoreDataMember]
        public ReadOnlyMemory<byte> Data { get; set; }

        public Batch()
        {
        }

        public Batch(List<IRequest> requests)
        {
            Requests = requests;
        }

        public override void Prepare(CommandPacker commandPacker)
        {
            base.Prepare(commandPacker);

            if (Requests != null && Requests.Count > 0)
            {
                foreach (var request in Requests)
                {
                    request.Prepare(commandPacker);
                }

                Data = commandPacker.Pack(Requests);
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

        public override void Restore(CommandPacker commandPacker)
        {
            base.Restore(commandPacker);

            using var stream = Data.AsStream();

            var task = commandPacker.Unpack(stream, CancellationToken.None);
            task.Wait();

            Requests = task.Result.Cast<IRequest>().ToList();

            Data = new ReadOnlyMemory<byte>();
        }
    }
}
