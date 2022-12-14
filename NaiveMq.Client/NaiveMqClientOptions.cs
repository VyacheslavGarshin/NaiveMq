using System;
using System.Net.Sockets;

namespace NaiveMq.Client
{
    public class NaiveMqClientOptions
    {
        public TcpClient TcpClient { get; set; }

        public string Host { get; set; } = "localhost";

        public int Port { get; set; } = 8506;

        public bool Autostart { get; set; } = true;

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
