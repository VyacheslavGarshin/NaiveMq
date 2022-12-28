﻿using CommunityToolkit.HighPerformance;
using NaiveMq.Client.Common;
using NaiveMq.Client.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NaiveMq.Client.Commands
{
    public class Batch : AbstractRequest<BatchResponse>, IDataCommand
    {
        [JsonIgnore]
        public List<IRequest> Requests { get; set; }

        /// <summary>
        /// Combined packed requests. Automatically generated from Requests on sending command.
        /// </summary>
        /// <remarks>When receive Data is reconstructed back to Requests. Then cleared.</remarks>
        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public Batch()
        {
        }

        public Batch(List<IRequest> requests)
        {
            Requests = requests;
        }

        public override void Prepare()
        {
            base.Prepare();

            if (Requests != null && Requests.Count > 0)
            {
                foreach (var request in Requests)
                {
                    request.Prepare();
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

        public override void Restore()
        {
            base.Restore();

            using var stream = Data.AsStream();

            var task = new CommandPacker(new JsonCommandConverter()).Unpack(stream, CancellationToken.None);
            task.Wait();

            Requests = task.Result.Cast<IRequest>().ToList();

            Data = new ReadOnlyMemory<byte>();
        }
    }
}
