using NaiveMq.Client.Commands;
using NaiveMq.Client.Converters;
using System.Buffers;
using System.Text;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace NaiveMq.Client.Common
{
    public partial class CommandPacker
    {
        private ICommandConverter _converter;

        public CommandPacker(ICommandConverter converter)
        {
            _converter = converter;
        }

        public PackResult Pack(ICommand command, ArrayPool<byte> arrayPool = null)
        {
            var commandNameBytes = Encoding.UTF8.GetBytes(command.GetType().Name);
            var commandBytes = _converter.Serialize(command);
            var dataLength = 0;
            var data = new ReadOnlyMemory<byte>();

            if (command is IDataCommand dataCommand)
            {
                dataLength = dataCommand.Data.Length;
                data = dataCommand.Data;
            }

            var allLength = 4 * 3 + commandNameBytes.Length + commandBytes.Length + dataLength;
            var buffer = arrayPool != null ? arrayPool.Rent(allLength) : new byte[allLength];

            buffer.CopyFrom(new[] {
                    BitConverter.GetBytes(commandNameBytes.Length),
                    BitConverter.GetBytes(commandBytes.Length),
                    BitConverter.GetBytes(dataLength),
                    commandNameBytes,
                    commandBytes,
                    data });

            return new PackResult { Buffer = buffer, Length = allLength };
        }

        public async Task<UnpackResult> Unpack(Stream stream, Action<UnpackResult> lengthCheckAction, CancellationToken cancellationToken, ArrayPool<byte> arrayPool = null)
        {
            byte[] lengthsBuffer = null;

            try
            {
                const int commandsBytesLength = 3 * 4;

                lengthsBuffer = arrayPool != null ? arrayPool.Rent(commandsBytesLength) : new byte[commandsBytesLength];
                await ReadStreamBytesAsync(stream, lengthsBuffer, commandsBytesLength, cancellationToken);

                var commandNameLength = BitConverter.ToInt32(lengthsBuffer, 0);
                var commandLength = BitConverter.ToInt32(lengthsBuffer, 4);
                var dataLength = BitConverter.ToInt32(lengthsBuffer, 8);

                var result = new UnpackResult { CommandNameLength = commandNameLength, CommandLength = commandLength, DataLength = dataLength };

                lengthCheckAction(result);

                var allLength = commandNameLength + commandLength + dataLength;
                var dataBuffer = arrayPool != null ? arrayPool.Rent(allLength) : new byte[allLength];
                await ReadStreamBytesAsync(stream, dataBuffer, allLength, cancellationToken);

                result.Buffer = dataBuffer;

                return result;
            }
            finally
            {
                if (lengthsBuffer != null && arrayPool != null)
                {
                    arrayPool.Return(lengthsBuffer);
                }
            }
        }

        private async Task<bool> ReadStreamBytesAsync(Stream stream, byte[] buffer, int length, CancellationToken cancellationToken)
        {
            var readLength = 0;
            var offset = 0;
            var size = length;

            do
            {
                var read = await stream.ReadAsync(buffer, offset, size, cancellationToken);

                if (read == 0)
                {
                    throw new IOException("Read 0 bytes.");
                }

                readLength += read;
                offset += read;
                size -= read;
            } while (readLength != length);

            return true;
        }
    }
}
