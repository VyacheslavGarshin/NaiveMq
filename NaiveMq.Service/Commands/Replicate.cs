using CommunityToolkit.HighPerformance;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Converters;
using Newtonsoft.Json;

namespace NaiveMq.Service.Commands
{
    public class Replicate : AbstractRequest<Confirmation>, IDataCommand
    {
        public string User { get; set; }

        [JsonIgnore]
        public IRequest Request { get; set; }

        /// <summary>
        /// Packed <see cref="Request"/>. Automatically generated from <see cref="Request"/> on sending replicate command.
        /// </summary>
        /// <remarks>When receive Data is reconstructed back to <see cref="Request"/>. Then cleared.</remarks>
        [JsonIgnore]
        public ReadOnlyMemory<byte> Data { get; set; }

        public Replicate()
        {
        }

        public Replicate(string user, IRequest command)
        {
            User = user;
            Request = command;
        }

        public override async Task PrepareAsync(CancellationToken cancellationToken)
        {
            await base.PrepareAsync(cancellationToken);

            if (Request != null)
            {
                await Request.PrepareAsync(cancellationToken);
                Data = new CommandPacker(new JsonCommandConverter()).Pack(Request).Buffer;
            }
        }

        public override void Validate()
        {
            base.Validate();

            if (Request is not IReplicable)
            {
                throw new ServerException(ErrorCode.NotReplicableRequest);
            }
        }

        public override async Task RestoreAsync(CancellationToken cancellationToken)
        {
            await base.RestoreAsync(cancellationToken);

            using var stream = Data.AsStream();

            Request = (await new CommandPacker(new JsonCommandConverter()).Unpack(stream, cancellationToken)).Cast<IRequest>().First();

            Data = new ReadOnlyMemory<byte>();
        }
    }
}