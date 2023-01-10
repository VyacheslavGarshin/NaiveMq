using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace NaiveMq.Client.Dto
{
    /// <summary>
    /// Host.
    /// </summary>
    [DataContract]
    public class Host
    {
        /// <summary>
        /// Name.
        /// </summary>
        [DataMember(Name = "N")]
        public string Name { get; set; }

        /// <summary>
        /// Port.
        /// </summary>
        [DataMember(Name = "P")]
        public int? Port { get; set; }

        /// <summary>
        /// Creates new Host.
        /// </summary>
        public Host()
        {

        }

        /// <summary>
        /// Creates new Host with params.
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="ArgumentException"></exception>
        public Host(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Cannot parse empty value into Address", nameof(value));
            }

            var split = value.Split(':', StringSplitOptions.RemoveEmptyEntries);

            Name = split[0];

            if (split.Length > 1)
            {
                if (int.TryParse(split[1], out var port))
                {
                    Port = port;
                }
                else
                {
                    throw new ArgumentException($"Cannot parse '{split[1]}' into Port", nameof(value));
                }
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Name}{(Port != null ? $":{Port}" : string.Empty)}";
        }

        /// <summary>
        /// Parse string to list of hosts.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IEnumerable<Host> Parse(string value)
        {
            return (value ?? string.Empty).Split(",;|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => new Host(x));
        }
    }
}
