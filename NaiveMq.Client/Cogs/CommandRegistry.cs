using Naive.Serializer.Cogs;
using NaiveMq.Client.Commands;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace NaiveMq.Client.Cogs
{
    /// <summary>
    /// Commands registry.
    /// </summary>
    public class CommandRegistry : IEnumerable<Type>
    {
        private readonly ConcurrentDictionary<string, Type> _commands = new(StringComparer.InvariantCultureIgnoreCase);

        private readonly ConcurrentDictionary<ReadOnlyMemory<byte>, Type> _commandsByBytes = new(new BytesComparer());

        /// <inheritdoc/>
        public IEnumerator<Type> GetEnumerator()
        {
            return _commands.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Add command type to registry.
        /// </summary>
        /// <param name="type"></param>
        /// <exception cref="ClientException"></exception>
        public void Add(Type type)
        {
            if (!_commands.TryAdd(type.Name, type))
            {
                throw new ClientException(ErrorCode.CommandAlreadyRegistered, new object[] { type.Name });
            }

            _commandsByBytes.TryAdd(Encoding.UTF8.GetBytes(type.Name), type);
        }

        /// <summary>
        /// Try get value by command name.
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="commandType"></param>
        /// <returns></returns>
        public bool TryGetValue(string commandName, out Type commandType)
        {
            return _commands.TryGetValue(commandName, out commandType); 
        }

        /// <summary>
        /// Try get value by UTF8 command name bytes.
        /// </summary>
        /// <param name="commandNameBytes"></param>
        /// <param name="commandType"></param>
        /// <returns></returns>
        public bool TryGetValue(ReadOnlyMemory<byte> commandNameBytes, out Type commandType)
        {
            return _commandsByBytes.TryGetValue(commandNameBytes, out commandType);
        }
    }
}
