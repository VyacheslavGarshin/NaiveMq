using NaiveMq.Client.Serializers;
using System;
using System.Net.Sockets;

namespace NaiveMq.Client
{
    public class NaiveMqClientOptions
    {
        public TcpClient TcpClient { get; set; }

        /// <summary>
        /// Comma/semicolon separated list of host[:port] values.
        /// </summary>
        /// <remarks>Default port is 8506.</remarks>
        public string Hosts { get; set; } = "localhost";

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Default is "guest".</remarks>
        public string Username { get; set; } = "guest";

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Default is "guest".</remarks>
        public string Password { get; set; } = "guest";

        /// <summary>
        /// Connect and login to the server on client creation.
        /// </summary>
        /// <remarks>Default is true.</remarks>
        public bool AutoStart { get; set; } = true;

        /// <summary>
        /// Auto restart on connection lost.
        /// </summary>
        public bool AutoRestart { get; set; } = true;

        /// <summary>
        /// Auto reconnect to another cluster server.
        /// </summary>
        public bool AutoClusterRedirect { get; set; } = true;

        public TimeSpan RestartInterval { get; set; } = TimeSpan.FromSeconds(5);

        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromMinutes(1);

        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Number of parallel tasks processing the incoming command.
        /// </summary>
        /// <remarks>Default value is 8.</remarks>
        public int Parallelism { get; set; } = 8;

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Default value is 1024.</remarks>
        public int MaxCommandNameSize { get; set; } = 1024;

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Default value is 1024 * 1024.</remarks>
        public int MaxCommandSize { get; set; } = 1024 * 1024;

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Default value is 100 * 1024 * 1024.</remarks>
        public int MaxDataSize { get; set; } = 100 * 1024 * 1024;

        public string CommandSerializer { get; set; } = nameof(NaiveCommandSerializer);

        /// <summary>
        /// Add this event before connect and login in constructor.
        /// </summary>
        public NaiveMqClient.OnStartHandler OnStart { get; set; }
        
        public NaiveMqClient.OnStopHandler OnStop { get; set; }

        public NaiveMqClientOptions Copy()
        {
            return new NaiveMqClientOptions
            {
                AutoClusterRedirect = AutoClusterRedirect,
                AutoRestart = AutoRestart,
                AutoStart = AutoStart,
                CommandSerializer = CommandSerializer,
                ConfirmTimeout = ConfirmTimeout,
                ConnectionTimeout = ConnectionTimeout,
                Hosts = Hosts,
                MaxCommandNameSize = MaxCommandNameSize,
                MaxCommandSize = MaxCommandSize,
                MaxDataSize = MaxDataSize,
                OnStart = OnStart,
                OnStop = OnStop,
                Parallelism = Parallelism,
                Password = Password,
                RestartInterval = RestartInterval,
                SendTimeout = SendTimeout,
                TcpClient = TcpClient,
                Username = Username,
            };
        }
    }
}
