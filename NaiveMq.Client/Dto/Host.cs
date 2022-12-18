using System;
using System.Collections.Generic;
using System.Linq;

namespace NaiveMq.Client.Dto
{
    public class Host
    {
        public string Name { get; set; }

        public int? Port { get; set; }

        public Host()
        {

        }

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
                try
                {
                    Port = int.Parse(split[1]);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Cannot parse '{split[1]}' into Port", nameof(value), ex);
                }
            }
        }

        public override string ToString()
        {
            return $"{Name}{(Port != null ? $":{Port}" : string.Empty)}";
        }

        public static IEnumerable<Host> Parse(string value)
        {
            return (value ?? string.Empty).Split(",;|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => new Host(x));
        }
    }
}
