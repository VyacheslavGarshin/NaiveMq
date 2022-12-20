using System;
using System.Net.Sockets;

namespace NaiveMq.Client
{
    public class NaiveMqClientOptions
    {
        public TcpClient TcpClient { get; set; }

        /// <summary>
        /// Comma/semicolon separated list of host:port values.
        /// </summary>
        public string Hosts { get; set; } = "localhost";

        public bool Autostart { get; set; } = true;

        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromMinutes(1);

        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Number of parallel tasks processing the incoming command.
        /// </summary>
        public int Parallelism { get; set; } = 8;

        public int MaxCommandNameSize { get; set; } = 1024;

        public int MaxCommandSize { get; set; } = 1024 * 1024;

        public int MaxDataSize { get; set; } = 100 * 1024 * 1024;

    }
}
