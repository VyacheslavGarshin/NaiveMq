using System;
using System.Net.Sockets;

namespace NaiveMq.Client
{
    public class NaiveMqClientOptions
    {
        public TcpClient TcpClient { get; set; }

        public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromMinutes(1);

        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromMinutes(1);

        public string Host { get; set; }

        public int Port { get; set; }

        public int Parallelism { get; set; } = 8;

        public int MaxCommandNameLength { get; set; } = 1024;

        public int MaxCommandLength { get; set; } = 1024 * 1024;

        public int MaxDataLength { get; set; } = 100 * 1024 * 1024;
    }
}
