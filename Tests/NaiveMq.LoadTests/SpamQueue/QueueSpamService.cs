using Microsoft.Extensions.Options;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Enums;
using NaiveMq.Service;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace NaiveMq.LoadTests.SpamQueue
{
    public class QueueSpamService : BackgroundService
    {
        private CancellationToken _stoppingToken;
        private ILogger<QueueSpamService> _logger;
        private QueueSpamServiceOptions _options;
        private readonly NaiveMqService _queueService;
        private readonly IServiceProvider _serviceProvider;
        private Timer _timer;

        public QueueSpamService(ILogger<QueueSpamService> logger, IServiceProvider serviceProvider, IOptions<QueueSpamServiceOptions> options, NaiveMqService queueService)
        {
            _logger = logger;
            _options = options.Value;
            _queueService = queueService;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;
            

            if (_options.IsEnabled)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_queueService.Online)
                    {
                        break;
                    }

                    await Task.Delay(1000, _stoppingToken);
                }

                if (_options.LogServerActivity)
                {
                    _timer = new Timer((s) =>
                    {
                        _queueService.Storage.Users[_options.Username].Queues.TryGetValue(_options.QueueName + "1", out var queue);

                        _logger.LogInformation($"{DateTime.Now:O};Read message/s;{_queueService.Counters.Read.Second.Value};" +
                            $"Write message/s;{_queueService.Counters.Write.Second.Value};" +
                            $"Read/s;{_queueService.Counters.ReadCommand.Second.Value};" +
                            $"Write/s;{_queueService.Counters.WriteCommand.Second.Value};" +
                            $"QueuesLength;{_queueService.Counters.Length.Value};" +
                            $"QueuesVolume;{_queueService.Counters.Volume.Value};");
                    }, null, 0, 1000);
                }

                await QueueSpamAsync();
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, _stoppingToken);
            }
        }

        private async Task QueueSpamAsync()
        {
            var clientLogger = _serviceProvider.GetRequiredService<ILogger<NaiveMqClient>>();

            await Task.Run(async () =>
            {
                await Task.Delay(1000);
                
                var taskCount = _options.ThreadsCount;
                var max = _options.MessageCount;

                var options = new NaiveMqClientOptions { Hosts = _options.Hosts, Parallelism = _options.Parallelism };

                using var c = new NaiveMqClient(options, clientLogger, _stoppingToken);

                // c.Start();

                if (!string.IsNullOrEmpty(_options.Username))
                {
                    await c.SendAsync(new Login { Username = _options.Username, Password = _options.Password }, _stoppingToken);
                }

                for (var queue = 1; queue <= _options.QueueCount; queue++)
                {
                    await CheckQueueCommandsAsync(c, queue);
                }

                await CheckUserCommandsAsync(c);

                await CheckExchangeAsync(c);

                var message = Encoding.UTF8.GetBytes(string.Join("", Enumerable.Range(0, _options.MessageLength).Select(x => "*")));

                var consumers = new List<NaiveMqClient>();

                await CreateConsumersAsync(clientLogger, taskCount, options, consumers);

                for (var run = 0; run < _options.Runs; run++)
                {
                    _logger.LogInformation($"Run {run + 1} is started.");

                    var swt = Stopwatch.StartNew();

                    RunProducers(clientLogger, taskCount, max, options, message);

                    _logger.LogInformation($"Run {run + 1} is ended. Sent {max * taskCount} messages in {swt.Elapsed}.");
                }

                await UnsubscribeAsync(c, consumers);

                for (var queue = 1; queue <= _options.QueueCount; queue++)
                {
                    if (_options.DeleteQueue)
                        await c.SendAsync(new DeleteQueue { Name = _options.QueueName + queue }, _stoppingToken);
                }
            });
        }

        private async Task UnsubscribeAsync(NaiveMqClient c, List<NaiveMqClient> consumers)
        {
            if (_options.Subscribe)
            {
                foreach (var consumer in consumers)
                {
                    for (var queue = 1; queue <= _options.QueueCount; queue++)
                    {
                        var queueName = _options.QueueName + queue;
                        await consumer.SendAsync(new Unsubscribe { Queue = queueName }, _stoppingToken);
                    }
                }
            }
        }

        private void RunProducers(ILogger<NaiveMqClient> clientLogger, int taskCount, int max, NaiveMqClientOptions options, byte[] message)
        {
            var tasks = new List<Task>();

            for (var queue = 1; queue <= _options.QueueCount; queue++)
            {
                var queueName = _options.QueueName + queue;

                for (var i = 0; i < taskCount; i++)
                {
                    var t = Task.Run(async () =>
                    {
                        var opts = JsonConvert.DeserializeObject<NaiveMqClientOptions>(JsonConvert.SerializeObject(options));

                        if (!string.IsNullOrEmpty(_options.ProducerHosts))
                        {
                            opts.Hosts = _options.ProducerHosts;
                        }

                        using var c = new NaiveMqClient(opts, clientLogger, _stoppingToken);

                        if (!string.IsNullOrEmpty(_options.Username))
                        {
                            await c.SendAsync(new Login { Username = _options.Username, Password = _options.Password }, _stoppingToken);
                        }

                        for (var j = 1; j <= max; j++)
                        {
                            await Produce(message, queueName, c);
                        }
                    });

                    tasks.Add(t);
                }
            }

            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Not all ended well: {ex.GetBaseException().Message}");
            }
        }

        private async Task CreateConsumersAsync(ILogger<NaiveMqClient> clientLogger, int taskCount, NaiveMqClientOptions options, List<NaiveMqClient> consumers)
        {
            if (_options.Subscribe)
            {
                var tasks = new List<Task>();

                for (var queue = 1; queue <= _options.QueueCount; queue++)
                {
                    var queueName = _options.QueueName + queue;

                    for (var i = 0; i < taskCount; i++)
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            var opts = JsonConvert.DeserializeObject<NaiveMqClientOptions>(JsonConvert.SerializeObject(options));

                            if (!string.IsNullOrEmpty(_options.ConsumerHosts))
                            {
                                opts.Hosts = _options.ConsumerHosts;
                            }

                            var client = new NaiveMqClient(opts, clientLogger, _stoppingToken);

                            if (!string.IsNullOrEmpty(_options.Username))
                            {
                                await client.SendAsync(new Login { Username = _options.Username, Password = _options.Password }, _stoppingToken);
                            }

                            await client.SendAsync(new Subscribe (queueName, _options.ConfirmSubscription, _options.ConfirmMessageTimeout, _options.ClusterStrategy), _stoppingToken);

                            Consume(client);

                            consumers.Add(client);
                        }));
                    }
                }

                await Task.WhenAll(tasks.ToArray());
            }
        }

        private async Task Produce(byte[] bytes, string queueName, NaiveMqClient c)
        {
            try
            {
                if (_options.Batch)
                {
                    var batch = new Batch
                    {
                        Requests = Enumerable.Range(0, _options.BatchSize).Select(x => CreateMessage(bytes, queueName) as IRequest).ToList()
                    };

                    var response = await c.SendAsync(batch, _options.Wait, _stoppingToken);
                }
                else
                {
                    var response = await c.SendAsync(CreateMessage(bytes, queueName), _options.Wait, _stoppingToken);
                }

                if (_options.SendDelay != null)
                {
                    await Task.Delay(_options.SendDelay.Value);
                }
            }
            catch (ClientException ex)
            {
                if (ex.ErrorCode == ErrorCode.ConfirmationTimeout)
                {
                    _logger.LogWarning(ex, "Spam confirmation timeout error");
                }
                else
                {
                    _logger.LogError(ex, "Spam send error");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Spam send error");
                throw;
            }
        }

        private Message CreateMessage(byte[] bytes, string queueName)
        {
            return new Message
            {
                Tag = $"Tag {DateTime.Now.ToShortTimeString()}",
                Queue = queueName,
                Persistent = _options.PersistentMessage,
                Request = _options.Request,
                Data = bytes,
                Confirm = _options.Confirm,
                ConfirmTimeout = _options.ConfirmTimeout,
            };
        }

        private void Consume(NaiveMqClient c)
        {
            c.OnReceiveMessageAsync += async (client, message) =>
            {
                if (_options.ReadBody)
                {
                    var body = message.Data.ToArray();
                }

                if (message.Confirm)
                {
                    try
                    {
                        await client.SendAsync(MessageResponse.Ok(message, message.Request ? Encoding.UTF8.GetBytes("Answer") : null, message.Request), _stoppingToken);
                    }
                    catch (ClientException)
                    {
                        // it's ok
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Spam send error");
                    }
                }

                if (_options.ReceiveDelay != null)
                {
                    await Task.Delay(_options.ReceiveDelay.Value);
                }
            };

            c.OnReceiveErrorAsync += (client, ex) =>
            {
                if (ex is not ClientException)
                {
                    _logger.LogError(ex, "Spam receive error");
                }

                return Task.CompletedTask;
            };
        }

        private async Task CheckExchangeAsync(NaiveMqClient c)
        {
            if (_options.AddExchange)
            {
                await c.SendAsync(new AddQueue { Name = _options.Exchange, Durable = true, Exchange = true }, _stoppingToken);
                await c.SendAsync(new AddQueue { Name = _options.ExchangeTo, Durable = true }, _stoppingToken);
            }

            if (_options.AddBinding)
            {
                await c.SendAsync(new AddBinding { Exchange = _options.Exchange, Queue = _options.ExchangeTo, Durable = true, Pattern = _options.BindingPattern }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.SendExchangeMessageWithKey))
            {
                await c.SendAsync(new Message { Queue = _options.Exchange, Confirm = true, Persistent = Persistence.Yes, RoutingKey = _options.SendExchangeMessageWithKey, Data = Encoding.UTF8.GetBytes("Some text to exchange") }, _stoppingToken);
            }

            if (_options.DeleteBinding)
            {
                await c.SendAsync(new DeleteBinding { Exchange = _options.Exchange, Queue = _options.ExchangeTo }, _stoppingToken);
            }
        }

        private async Task CheckQueueCommandsAsync(NaiveMqClient c, int queue)
        {
            var queueName = _options.QueueName + queue;

            if (_options.AddQueue)
            {
                if ((await c.SendAsync(new GetQueue { Name = queueName, Try = true }, _stoppingToken)).Entity != null)
                {
                    if (_options.RewriteQueue)
                    {
                        await c.SendAsync(new DeleteQueue { Name = queueName }, _stoppingToken);
                    }
                }

                await c.SendAsync(new AddQueue
                {
                    Name = queueName,
                    Durable = _options.Durable,
                    LengthLimit = _options.LengthLimit,
                    VolumeLimit = _options.VolumeLimit,
                    LimitStrategy = _options.LimitStrategy,
                }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.SearchQueues))
            {
                await c.SendAsync(new SearchQueues { Name = _options.SearchQueues }, _stoppingToken);
            }

            if (_options.ClearQueue)
            {
                await c.SendAsync(new ClearQueue { Name = queueName }, _stoppingToken);
            }
        }

        private async Task CheckUserCommandsAsync(NaiveMqClient c)
        {
            if (_options.GetProfile)
            {
                await c.SendAsync(new GetProfile(), _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.ChangePassword))
            {
                await c.SendAsync(new ChangePassword { CurrentPassword = _options.Password, NewPassword = _options.ChangePassword }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.GetUser))
            {
                await c.SendAsync(new GetUser { Username = _options.AddUser, Try = _options.GetUserTry }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.SearchUsers))
            {
                await c.SendAsync(new SearchUsers { Username = _options.SearchUsers }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.AddUser))
            {
                await c.SendAsync(new AddUser { Username = _options.AddUser, Administrator = true, Password = "guest" }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.UpdateUser))
            {
                await c.SendAsync(new UpdateUser { Username = _options.UpdateUser, Administrator = true }, _stoppingToken);
            }

            if (_options.DeleteUser)
            {
                await c.SendAsync(new DeleteUser { Username = _options.AddUser }, _stoppingToken);
            }
        }
    }
}