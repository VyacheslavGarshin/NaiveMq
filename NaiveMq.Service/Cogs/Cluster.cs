using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class Cluster : IDisposable
    {
        public bool Started { get; private set; }

        private Timer _discoveryTimer;
        
        private readonly NaiveMqServiceOptions _options;
        
        private readonly ILogger<NaiveMqService> _logger;
        
        private readonly ILogger<NaiveMqClient> _clientLogger;

        private readonly CancellationToken _stoppingToken;

        private readonly ConcurrentDictionary<string, ClientContext> _clients = new(StringComparer.InvariantCultureIgnoreCase);

        private readonly ConcurrentDictionary<string, string> _hostsMap = new(StringComparer.InvariantCultureIgnoreCase);

        public Cluster(NaiveMqServiceOptions options, ILogger<NaiveMqService> logger, ILogger<NaiveMqClient> clientLogger, CancellationToken stoppingToken)
        {
            _options = options;
            _logger = logger;
            _clientLogger = clientLogger;
            _stoppingToken = stoppingToken;
        }

        public void Start()
        {
            if (!Started && !string.IsNullOrWhiteSpace(_options.ClusterHosts) 
                && !string.IsNullOrWhiteSpace(_options.ClusterUser) 
                && !string.IsNullOrWhiteSpace(_options.ClusterUserPassword))
            {
                _discoveryTimer = new Timer((state) => { Task.Run(async () => { await ClusterDiscovery(); }); }, null, TimeSpan.Zero, _options.ClusterDiscoveryInterval);

                Started = true;
                _logger.LogInformation("Cluster discovery started with hosts '{ClusterHosts}' and user '{ClusterUser}'.", _options.ClusterHosts, _options.ClusterUser);
            }
        }

        public void Stop()
        {
            if (Started)
            {
                if (_discoveryTimer != null)
                {
                    _discoveryTimer.Dispose();
                    _discoveryTimer = null;
                }

                Started = false;
            }
        }

        public void Add(ClientContext clientContext)
        {

        }

        public void Remove(ClientContext clientContext)
        {

        }

        public void Dispose()
        {
            Stop();
            _clients.Clear();
        }

        private async Task ClusterDiscovery()
        {
            try
            {
                _discoveryTimer.Change(0, Timeout.Infinite);
                
                var tasks = new List<Task>();

                foreach (var host in Host.Parse(_options.ClusterHosts))
                {
                    tasks.Add(Task.Run(async () => { await DiscoverHost(host); }));
                }

                await Task.WhenAll(tasks.ToArray());
            }
            finally
            {
                _discoveryTimer.Change(TimeSpan.Zero, _options.ClusterDiscoveryInterval);
            }
        }

        private async Task DiscoverHost(Host host)
        {
            try
            {
                using var client = new NaiveMqClient(new NaiveMqClientOptions { Hosts = host.ToString(), Autostart = false }, _clientLogger, _stoppingToken);

                client.Start();

                var server = await client.SendAsync(new GetServer());

                if (server.Entity.Name == _options.Name)
                {
                    return;
                }

                _hostsMap.AddOrUpdate(host.ToString(), (key) => server.Entity.Name, (key, value) => server.Entity.Name);

                await client.SendAsync(new Login { Username = _options.ClusterUser, Password = _options.ClusterUserPassword });
            }
            catch (Exception)
            {

            }
        }
    }
}