using NaiveMq.Client.Commands;
using NaiveMq.Client.Converters;
using System.Buffers;
using System.Text;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace NaiveMq.Client.Common
{
    public class CommandPacker
    {
        public ArrayPool<byte> ArrayPool { get; }

        private readonly ICommandSerializer _converter;        

        public CommandPacker(ICommandSerializer converter, ArrayPool<byte> arrayPool)
        {
            _converter = converter;
            ArrayPool = arrayPool;
        }

        public PackResult Pack(ICommand command)
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
            var buffer = ArrayPool.Rent(allLength);

            buffer.CopyFrom(new[] {
                    BitConverter.GetBytes(commandNameBytes.Length),
                    BitConverter.GetBytes(commandBytes.Length),
                    BitConverter.GetBytes(dataLength),
                    commandNameBytes,
                    commandBytes,
                    data });

            return new PackResult { Buffer = buffer, Length = allLength };
        }

        public byte[] Pack(IEnumerable<ICommand> commands)
        {
            var packResults = new List<PackResult>();

            try
            {
                foreach (var request in commands)
                {
                    packResults.Add(Pack(request));
                }

                var data = new byte[packResults.Sum(x => x.Length)];
                data.CopyFrom(packResults.Select(x => new ReadOnlyMemory<byte>(x.Buffer, 0, x.Length)));

                return data;
            }
            finally
            {
                foreach (var buffer in packResults.Select(x => x.Buffer))
                {
                    ArrayPool.Return(buffer);
                }
            }
        }

        public async Task<UnpackResult> Unpack(Stream stream, Action<UnpackResult> lengthCheckAction, CancellationToken cancellationToken)
        {
            byte[] lengthsBuffer = null;

            try
            {
                const int commandsBytesLength = 3 * 4;

                lengthsBuffer = ArrayPool.Rent(commandsBytesLength);
                await ReadStreamBytesAsync(stream, lengthsBuffer, commandsBytesLength, cancellationToken);

                var commandNameLength = BitConverter.ToInt32(lengthsBuffer, 0);
                var commandLength = BitConverter.ToInt32(lengthsBuffer, 4);
                var dataLength = BitConverter.ToInt32(lengthsBuffer, 8);

                var result = new UnpackResult { CommandNameLength = commandNameLength, CommandLength = commandLength, DataLength = dataLength };

                lengthCheckAction?.Invoke(result);

                var allLength = commandNameLength + commandLength + dataLength;
                var dataBuffer = ArrayPool.Rent(allLength);
                await ReadStreamBytesAsync(stream, dataBuffer, allLength, cancellationToken);

                result.Buffer = dataBuffer;

                return result;
            }
            finally
            {
                if (lengthsBuffer != null)
                {
                    ArrayPool.Return(lengthsBuffer);
                }
            }
        }

        public async Task<List<ICommand>> Unpack(Stream stream, CancellationToken cancellationToken)
        {
            var result = new List<ICommand>();
            var unpackResults = new List<UnpackResult>();

            try
            {
                while (stream.Position < stream.Length)
                {
                    unpackResults.Add(await Unpack(stream, null, cancellationToken));
                }

                foreach (var unpackResult in unpackResults)
                {
                    result.Add(CreateCommand(unpackResult));
                }
            }
            finally
            {
                foreach (var buffer in unpackResults.Select(x => x.Buffer))
                {
                    ArrayPool.Return(buffer);
                }
            }

            return result;
        }

        public ICommand CreateCommand(UnpackResult unpackResult)
        {
            var index = 0;
            var commandNameBytes = new ReadOnlyMemory<byte>(unpackResult.Buffer, index, unpackResult.CommandNameLength);
            index += unpackResult.CommandNameLength;
            var commandBytes = new ReadOnlyMemory<byte>(unpackResult.Buffer, index, unpackResult.CommandLength);
            index += unpackResult.CommandLength;
            var dataBytes = unpackResult.DataLength > 0 ? new ReadOnlyMemory<byte>(unpackResult.Buffer, index, unpackResult.DataLength) : new ReadOnlyMemory<byte>();

            var commandName = Encoding.UTF8.GetString(commandNameBytes.Span);
            var commandType = GetCommandType(commandName);

            var command = ParseCommand(commandBytes, commandType);

            if (command is IDataCommand dataCommand)
            {
                dataCommand.Data = dataBytes;
            }

            return command;
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

        private Type GetCommandType(string commandName)
        {
            if (NaiveMqClient.CommandTypes.TryGetValue(commandName, out Type commandType))
            {
                return commandType;
            }
            else
            {
                throw new ClientException(ErrorCode.CommandNotFound, new object[] { commandName });
            }
        }

        private ICommand ParseCommand(ReadOnlyMemory<byte> commandBytes, Type commandType)
        {
            var result = _converter.Deserialize(commandBytes, commandType);

            if (result.Id == Guid.Empty)
            {
                throw new ClientException(ErrorCode.EmptyCommandId);
            }

            return result;
        }
    }
}
