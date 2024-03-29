﻿using NaiveMq.Client.Commands;
using System.Buffers;
using System.Text;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using NaiveMq.Client.Serializers;

namespace NaiveMq.Client.Cogs
{
    /// <summary>
    /// Command packer.
    /// </summary>
    public class CommandPacker
    {
        /// <summary>
        /// Array pool.
        /// </summary>
        public ArrayPool<byte> ArrayPool { get; }

        private readonly ICommandSerializer _commandSerializer;

        /// <summary>
        /// Creates new CommandPacker.
        /// </summary>
        /// <param name="commandSerializer"></param>
        /// <param name="arrayPool"></param>
        public CommandPacker(ICommandSerializer commandSerializer, ArrayPool<byte> arrayPool)
        {
            _commandSerializer = commandSerializer;
            ArrayPool = arrayPool;
        }

        /// <summary>
        /// Pack command.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public BufferResult Pack(ICommand command)
        {
            BufferResult commandPackResult = null;

            try
            {
                var commandNameBytes = Encoding.UTF8.GetBytes(command.GetType().Name);
                commandPackResult = _commandSerializer.Serialize(command, ArrayPool);
                var dataLength = 0;
                var data = new ReadOnlyMemory<byte>();

                if (command is IDataCommand dataCommand)
                {
                    dataLength = dataCommand.Data.Length;
                    data = dataCommand.Data;
                }

                var allLength = 4 * 3 + commandNameBytes.Length + commandPackResult.Length + dataLength;
                var buffer = ArrayPool.Rent(allLength);

                buffer.CopyFrom(new[] {
                    BitConverter.GetBytes(commandNameBytes.Length),
                    BitConverter.GetBytes(commandPackResult.Length),
                    BitConverter.GetBytes(dataLength),
                    commandNameBytes,
                    new ReadOnlyMemory<byte>(commandPackResult.Buffer, 0, commandPackResult.Length),
                    data });

                return new BufferResult(buffer, allLength);
            }
            finally
            {
                if (commandPackResult != null)
                {
                    ArrayPool.Return(commandPackResult.Buffer);
                }
            }
        }

        /// <summary>
        /// Pack commands.
        /// </summary>
        /// <param name="commands"></param>
        /// <returns></returns>
        public byte[] Pack(IEnumerable<ICommand> commands)
        {
            var packResults = new List<BufferResult>();

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

        /// <summary>
        /// Read stream and return buffer and command, data lengths. 
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="lengthCheckAction"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<CommandReadResult> ReadAsync(Stream stream, Action<CommandReadResult> lengthCheckAction, CancellationToken cancellationToken)
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

                var result = new CommandReadResult { CommandNameLength = commandNameLength, CommandLength = commandLength, DataLength = dataLength };

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

        /// <summary>
        /// Read stream and unpack commands within into list.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<ICommand>> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            var result = new List<ICommand>();
            var unpackResults = new List<CommandReadResult>();

            try
            {
                while (stream.Position < stream.Length)
                {
                    unpackResults.Add(await ReadAsync(stream, null, cancellationToken));
                }

                foreach (var unpackResult in unpackResults)
                {
                    result.Add(Unpack(unpackResult));
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

        /// <summary>
        /// Unpack command from buffer.
        /// </summary>
        /// <param name="unpackResult"></param>
        /// <returns></returns>
        public ICommand Unpack(CommandReadResult unpackResult)
        {
            var index = 0;
            var commandNameBytes = new ReadOnlyMemory<byte>(unpackResult.Buffer, index, unpackResult.CommandNameLength);
            index += unpackResult.CommandNameLength;
            var commandBytes = new ReadOnlyMemory<byte>(unpackResult.Buffer, index, unpackResult.CommandLength);
            index += unpackResult.CommandLength;
            var dataBytes = unpackResult.DataLength > 0 ? new ReadOnlyMemory<byte>(unpackResult.Buffer, index, unpackResult.DataLength) : new ReadOnlyMemory<byte>();

            var commandType = GetCommandType(commandNameBytes);

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

        private Type GetCommandType(ReadOnlyMemory<byte> commandNameBytes)
        {
            if (NaiveMqClient.Commands.TryGetValue(commandNameBytes, out Type commandType))
            {
                return commandType;
            }
            else
            {
                throw new ClientException(ErrorCode.CommandNotFound, new object[] { Encoding.UTF8.GetString(commandNameBytes.Span) });
            }
        }

        private ICommand ParseCommand(ReadOnlyMemory<byte> commandBytes, Type commandType)
        {
            var result = (ICommand)_commandSerializer.Deserialize(commandBytes, commandType);

            if (result.Id == Guid.Empty)
            {
                throw new ClientException(ErrorCode.EmptyCommandId);
            }

            return result;
        }
    }
}
