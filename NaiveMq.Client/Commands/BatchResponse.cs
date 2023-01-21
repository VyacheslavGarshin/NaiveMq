using Naive.Serializer.Cogs;
using NaiveMq.Client.AbstractCommands;
using NaiveMq.Client.Cogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace NaiveMq.Client.Commands
{
    /// <summary>
    /// Batch response.
    /// </summary>
    public class BatchResponse : AbstractResponse<BatchResponse>, IDataCommand
    {
        /// <summary>
        /// Responses.
        /// </summary>
        [IgnoreDataMember]
        public List<IResponse> Responses { get; set; }

        /// <summary>
        /// Combined packed responses. Automatically generated from Responses on sending command.
        /// </summary>
        /// <remarks>When receive Data is reconstructed back to Responses. Then cleared.</remarks>
        [IgnoreDataMember]
        public ReadOnlyMemory<byte> Data { get; set; }

        /// <summary>
        /// Creates new BatchResponse command.
        /// </summary>
        public BatchResponse()
        {
        }

        /// <summary>
        /// Creates new BatchResponse command with params.
        /// </summary>
        /// <param name="responses"></param>
        public BatchResponse(List<IResponse> responses)
        {
            Responses = responses;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override void Restore(CommandPacker commandPacker)
        {
            base.Restore(commandPacker);

            // RomStream is slow
            using var stream = new MemoryStream(Data.ToArray());

            var task = commandPacker.ReadAsync(stream, CancellationToken.None);
            task.Wait();

            Responses = task.Result.Cast<IResponse>().ToList();

            Data = new ReadOnlyMemory<byte>();
        }

        /// <inheritdoc/>
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
    }
}
