using Naive.Serializer.Cogs;
using NaiveMq.Client.Cogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Batch request.
    /// </summary>
    public class Batch : AbstractRequest<BatchResponse>, IDataCommand
    {
        /// <summary>
        /// Requests.
        /// </summary>
        [IgnoreDataMember]
        public List<IRequest> Requests { get; set; }

        /// <summary>
        /// Combined packed requests. Automatically generated from Requests on sending command.
        /// </summary>
        /// <remarks>When receive Data is reconstructed back to Requests. Then cleared.</remarks>
        [IgnoreDataMember]
        public ReadOnlyMemory<byte> Data { get; set; }

        /// <summary>
        /// Creates new Batch command.
        /// </summary>
        public Batch()
        {
        }

        /// <summary>
        /// Creates new Batch command with params.
        /// </summary>
        /// <param name="requests"></param>
        public Batch(List<IRequest> requests)
        {
            Requests = requests;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override void Restore(CommandPacker commandPacker)
        {
            base.Restore(commandPacker);

            using var stream = new RomStream(Data);

            var task = commandPacker.ReadAsync(stream, CancellationToken.None);
            task.Wait();

            Requests = task.Result.Cast<IRequest>().ToList();

            Data = new ReadOnlyMemory<byte>();
        }

        /// <inheritdoc/>
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
    }
}
