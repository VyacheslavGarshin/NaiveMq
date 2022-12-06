using System;
using System.Net.Sockets;

namespace NaiveMq.Client
{
    public class NaiveMqClientOptions
    {
        public TcpClient TcpClient { get; set; }

        public TimeSpan? ConfirmTimeout { get; set; }

        public string Host { get; set; }

        public int? Port { get; set; }

        public int Parallelism { get; set; } = 8;
    }
}
