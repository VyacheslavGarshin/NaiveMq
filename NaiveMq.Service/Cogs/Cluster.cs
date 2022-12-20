using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;
using NaiveMq.Service.Commands;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class Cluster : IDisposable
    {
        public bool Started { get; private set; }

        private Timer _discoveryTimer;
        
        private readonly Storage _storage;
        
        private readonly ILogger<NaiveMqService> _logger;
        
        private readonly ILogger<NaiveMqClient> _clientLogger;

        private readonly CancellationToken _stoppingToken;
        
        private readonly NaiveMqServiceOptions _options;
        
        private readonly ConcurrentDictionary<string, ClusterServer> _servers = new(StringComparer.InvariantCultureIgnoreCase);

        public Cluster(Storage storage, ILogger<NaiveMqService> logger, ILogger<NaiveMqClient> clientLogger, CancellationToken stoppingToken)
        {
            _storage = storage;
            _logger = logger;
            _clientLogger = clientLogger;
            _stoppingToken = stoppingToken;
            _options = _storage.Service.Options;
        }

        public void Start()
        {
            if (!Started && !string.IsNullOrWhiteSpace(_options.ClusterHosts) 
                && !string.IsNullOrWhiteSpace(_options.ClusterAdmin) 
                && !string.IsNullOrWhiteSpace(_options.ClusterAdminPassword))
            {
                _discoveryTimer = new Timer((state) => { Task.Run(async () => { await ClusterDiscovery(); }); }, null, TimeSpan.Zero, _options.ClusterDiscoveryInterval);

                Started = true;
                _logger.LogInformation("Cluster discovery started with hosts '{ClusterHosts}' and user '{ClusterUser}'.", _options.ClusterHosts, _options.ClusterAdmin);
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

                foreach (var server in _servers.Values)
                {
                    if (server.Client != null)
                    {
                        server.Client.Dispose();
                    }
                }

                _servers.Clear();

                Started = false;
            }
        }

        public void HandleRequest(IRequest request, ClientContext clientContext)
        {
            if (request is IReplicable)
            {
                _ = Task.Run(async () => { await ReplicateRequest(request, clientContext); });
            };
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task ClusterDiscovery()
        {
            try
            {
                _discoveryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                
                var tasks = new List<Task>();

                foreach (var host in Host.Parse(_options.ClusterHosts))
                {
                    if (!_servers.TryGetValue(host.ToString(), out var server) || server == null)
                    {
                        tasks.Add(Task.Run(async () => { await DiscoverHost(host); }));
                    }
                }

                await Task.WhenAll(tasks.ToArray());
            }
            finally
            {
                _discoveryTimer.Change(_options.ClusterDiscoveryInterval, _options.ClusterDiscoveryInterval);
            }
        }

        private async Task DiscoverHost(Host host)
        {
            NaiveMqClient client = null;

            try
            {
                client = new NaiveMqClient(new NaiveMqClientOptions { Hosts = host.ToString(), Autostart = false }, _clientLogger, _stoppingToken);
                client.OnStop += Client_OnStop;
                client.Start();

                var getServerResponse = await client.SendAsync(new GetServer());

                if (getServerResponse.Entity.ClusterKey != _options.ClusterKey)
                {
                    throw new ServerException(ErrorCode.ClusterKeysDontMatch);
                }

                var server = new ClusterServer { Name = getServerResponse.Entity.Name, Self = getServerResponse.Entity.Name == _options.Name };
                _servers.AddOrUpdate(host.ToString(), (key) => server, (key, value) => server);

                if (!server.Self)
                {
                    await client.SendAsync(new Login { Username = _options.ClusterAdmin, Password = _options.ClusterAdminPassword });

                    server.Client = client;
                    client = null;

                    _logger.LogInformation("Discovered cluster server '{Host}', name '{Name}'.", host, server.Name);
                }
            }
            catch (ClientException ex)
            {
                if (ex.ErrorCode == ErrorCode.HostsUnavailable)
                {
                    return;
                }

                _logger.LogError(ex, "Client error while discovering cluster server {Host}.", host);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while discovering cluster server {Host}.", host);
                throw;
            }
            finally
            {
                if (client != null)
                {
                    client.Dispose();
                }
            }
        }

        private void Client_OnStop(NaiveMqClient sender)
        {
            var server = _servers.FirstOrDefault(x => x.Value?.Client?.Id == sender.Id);

            if (server.Key != null)
            {
                _servers.Remove(server.Key, out var _);
                server.Value.Client.Dispose();
                server.Value.Client = null;

                _logger.LogInformation("Removed cluster server '{Host}', name '{Name}'.", server.Key, server.Value.Name);
            }
        }

        private async Task ReplicateRequest(IRequest request, ClientContext clientContext)
        {
            foreach (var server in _servers.Values.Where(x => !x.Self && x.Client != null))
            {
                try
                {
                    await server.Client.SendAsync(new Replicate(clientContext.User.Entity.Username, request));
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Error while replicating {CommandType} request with Id {Id}.", request.GetType().Name, request.Id);
                }
            }
        }

        private class ClusterServer
        {
            public string Name { get; set; }

            public bool Self { get; set; }

            public NaiveMqClient Client { get; set; }
        }
    }
}