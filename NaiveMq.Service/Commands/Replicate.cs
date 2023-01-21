using Naive.Serializer.Cogs;
using NaiveMq.Client;
using NaiveMq.Client.AbstractCommands;
using NaiveMq.Client.Cogs;
using NaiveMq.Client.Commands;
using System.Runtime.Serialization;

namespace NaiveMq.Service.Commands
{
    public class Replicate : AbstractRequest<Confirmation>, IDataCommand
    {
        public string User { get; set; }

        [IgnoreDataMember]
        public IRequest Request { get; set; }

        /// <summary>
        /// Packed <see cref="Request"/>. Automatically generated from <see cref="Request"/> on sending replicate command.
        /// </summary>
        /// <remarks>When receive Data is reconstructed back to <see cref="Request"/>. Then cleared.</remarks>
        [IgnoreDataMember]
        public ReadOnlyMemory<byte> Data { get; set; }

        public Replicate()
        {
        }

        public Replicate(string user, IRequest command)
        {
            User = user;
            Request = command;
        }

        public override void Prepare(CommandPacker commandPacker)
        {
            base.Prepare(commandPacker);

            if (Request != null)
            {
                Request.Prepare(commandPacker);

                BufferResult packResult = null;

                try
                {
                    packResult = commandPacker.Pack(Request);
                    var data = new byte[packResult.Length];                    
                    packResult.Buffer.CopyTo(data, 0);
                    Data = data;
                }
                finally
                {
                    if (packResult != null)
                    {
                        commandPacker.ArrayPool.Return(packResult.Buffer);
                    }
                }
            }
        }

        public override void Restore(CommandPacker commandPacker)
        {
            base.Restore(commandPacker);

            using var stream = new RomStream(Data);

            var task = commandPacker.ReadAsync(stream, CancellationToken.None);
            task.Wait();

            Request = task.Result.Cast<IRequest>().First();

            Data = new ReadOnlyMemory<byte>();
        }

        public override void Validate()
        {
            base.Validate();

            if (Request is not IReplicable)
            {
                throw new ServerException(ErrorCode.NotReplicableRequest);
            }
        }
    }
}