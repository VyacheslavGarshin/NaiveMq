using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Dto;
using NaiveMq.Service.Commands;
using NaiveMq.Service.Dto;
using NaiveMq.Service.Enums;
using System.Collections.Concurrent;

namespace NaiveMq.Service.Cogs
{
    public class Cluster : IDisposable
    {
        private static readonly string[] _goodErrors = new[] { "AlreadyExists", "NotFound" };

        public bool Started { get; private set; }

        public ConcurrentDictionary<string, ClusterServer> Servers { get; } = new(StringComparer.InvariantCultureIgnoreCase);

        public ClusterServer Self { get; private set; }

        private Timer _discoveryTimer;

        private Timer _statsTimer;

        private readonly Storage _storage;
        
        private readonly ILogger<NaiveMqService> _logger;
        
        private readonly ILogger<NaiveMqClient> _clientLogger;

        private readonly CancellationToken _stoppingToken;
        
        private readonly NaiveMqServiceOptions _options;
        
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
                && !string.IsNullOrWhiteSpace(_options.ClusterAdminUsername) 
                && !string.IsNullOrWhiteSpace(_options.ClusterAdminPassword))
            {
                _discoveryTimer = new Timer(async (state) => { await DiscoverHosts(); }, null, TimeSpan.Zero, _options.ClusterDiscoveryInterval);

                _statsTimer = new Timer(async (state) => { await SendServerStats(); }, null, TimeSpan.Zero, _options.ClusterStatsInterval);

                Started = true;
                _logger.LogInformation("Cluster discovery started with hosts '{ClusterHosts}' and cluster admin '{ClusterAdmin}'.", _options.ClusterHosts, _options.ClusterAdminUsername);
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

                if (_statsTimer != null)
                {
                    _statsTimer.Dispose();
                    _statsTimer = null;
                }

                foreach (var server in Servers.Values)
                {
                    server.Dispose();
                }

                Servers.Clear();

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

        private async Task DiscoverHosts()
        {
            try
            {
                _discoveryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                
                var tasks = new List<Task>();

                foreach (var host in Host.Parse(_options.ClusterHosts))
                {
                    if (!Servers.TryGetValue(host.ToString(), out var server) || !server.Self && server.Client == null)
                    {
                        tasks.Add(Task.Run(async () => { await DiscoverHost(host); }));
                    }
                }

                await Task.WhenAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception while discovering cluster servers.");
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
                client.Start(false);

                var getServerResponse = await client.SendAsync(new GetServer());

                if (getServerResponse.Entity.ClusterKey != _options.ClusterKey)
                {
                    throw new ServerException(ErrorCode.ClusterKeysDontMatch);
                }

                var server = new ClusterServer
                {
                    Host = host,
                    Name = getServerResponse.Entity.Name,
                    Self = getServerResponse.Entity.Name == _options.Name
                };
                Servers.AddOrUpdate(host.ToString(), (key) => server, (key, value) => server);

                if (!server.Self)
                {
                    await client.SendAsync(new Login { Username = _options.ClusterAdminUsername, Password = _options.ClusterAdminPassword });

                    server.Client = client;
                    client = null;

                    _logger.LogInformation("Discovered cluster server '{Host}', name '{Name}'.", host, server.Name);
                }
                else
                {
                    Self = server;
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

        private async Task SendServerStats()
        {
            try
            {
                _discoveryTimer.Change(Timeout.Infinite, Timeout.Infinite);

                if (Self == null)
                {
                    return;
                }

                var tasks = new List<Task>();

                var queues = _storage.Users.Values.SelectMany(x => x.Queues.Values)
                    .Where(x => x.Status == QueueStatus.Started && x.Length > 0).ToArray();

                Self.ReplaceActiveQueues(queues.Select(x => new ActiveQueue
                {
                    User = x.Entity.User,
                    Name = x.Entity.Name,
                    Length = x.Length,
                    Subscriptions = x.Counters.Subscriptions.Value
                }));

                foreach (var server in Servers.Values.Where(x => !x.Self && x.Client != null))
                {
                    tasks.Add(Task.Run(async () => { await SendServerStats(server, queues); }));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception while send stats.");
            }
            finally
            {
                _discoveryTimer.Change(_options.ClusterStatsInterval, _options.ClusterStatsInterval);
            }
        }

        private async Task SendServerStats(ClusterServer server, IEnumerable<QueueCog> queues)
        {
            try
            {
                await server.Client.SendAsync(new ServerActitvity(Self.Name, true, false));

                ServerActitvity serverStats = null;

                foreach (var queue in queues)
                {
                    serverStats ??= new ServerActitvity(Self.Name, false, false, new());

                    if (serverStats.ActiveQueues.Count < _storage.Service.Options.ClusterStatsBatchSize)
                    {
                        serverStats.ActiveQueues.Add(new ActiveQueue(
                            queue.Entity.User,
                            queue.Entity.Name,
                            queue.Length,
                            queue.Counters.Subscriptions.Value));
                    }
                    else
                    {
                        await server.Client.SendAsync(serverStats);
                        serverStats = null;
                    }
                }

                if (serverStats != null)
                {
                    await server.Client.SendAsync(serverStats);
                }

                await server.Client.SendAsync(new ServerActitvity(Self.Name, false, true));
            }
            catch (ClientException ex)
            {
                if (ex.ErrorCode != ErrorCode.ServerNotFound)
                {
                    _logger.LogError(ex, "Unexpected client exception while send stats for server '{Host}'.", server.Host);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception while send stats for server '{Host}'.", server.Host);
            }
        }

        private void Client_OnStop(NaiveMqClient sender)
        {
            var server = Servers.Values.FirstOrDefault(x => x.Client?.Id == sender.Id);

            if (server != null)
            {
                server.Client.Dispose();
                server.Client = null;

                _logger.LogInformation("Disconnected cluster server '{Host}', name '{Name}'.", server.Host, server.Name);
            }
        }

        private async Task ReplicateRequest(IRequest request, ClientContext clientContext)
        {
            foreach (var server in Servers.Values.Where(x => !x.Self && x.Client != null))
            {
                try
                {
                    await server.Client.SendAsync(new Replicate(clientContext.User.Entity.Username, request));
                }
                catch (ClientException ex)
                {
                    // we expect not all data where replicated, especially not Durable things.
                    if (!_goodErrors.Any(x => (ex?.Response?.ErrorCode ?? string.Empty).Contains(x, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        _logger.LogError(ex, "Unexpected client error while replicating {CommandType} request with Id {Id}.", request.GetType().Name, request.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while replicating {CommandType} request with Id {Id}.", request.GetType().Name, request.Id);
                }
            }
        }
    }
}