using NaiveMq.Client.Serializers;
using System;
using System.Net.Sockets;

namespace NaiveMq.Client
{
    /// <summary>
    /// NaiveMq client options.
    /// </summary>
    public class NaiveMqClientOptions
    {
        /// <summary>
        /// Ready TcpClient.
        /// </summary>
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

        /// <summary>
        /// Restart interval.
        /// </summary>
        public TimeSpan RestartInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Connection timeout.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Command confirm timeout.
        /// </summary>
        public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Command send timeout.
        /// </summary>
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

        /// <summary>
        /// Command serializer used in communication.
        /// </summary>
        /// <remarks>Default is <see cref="NaiveCommandSerializer"/>.</remarks>
        public string CommandSerializer { get; set; } = nameof(NaiveCommandSerializer);

        /// <summary>
        /// On start.
        /// </summary>
        /// <remarks>Add this event before connect and login in constructor.</remarks>
        public NaiveMqClient.OnStartHandler OnStart { get; set; }
        
        /// <summary>
        /// On stop.
        /// </summary>
        public NaiveMqClient.OnStopHandler OnStop { get; set; }

        /// <summary>
        /// Creates new NaiveMq client options.
        /// </summary>
        public NaiveMqClientOptions()
        {
        }

        /// <summary>
        /// Creates new NaiveMq client options.
        /// </summary>
        /// <param name="hosts"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="onStart"></param>
        /// <param name="onStop"></param>
        public NaiveMqClientOptions(
            string hosts, 
            string username, 
            string password, 
            NaiveMqClient.OnStartHandler onStart = null, 
            NaiveMqClient.OnStopHandler onStop = null)
        {
            Hosts = hosts;
            Username = username;
            Password = password;
            OnStart = onStart;
            OnStop = onStop;
        }

        /// <summary>
        /// Copy options.
        /// </summary>
        /// <returns></returns>
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
