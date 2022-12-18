﻿using Microsoft.Extensions.Options;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Enums;
using NaiveMq.Service;
using System.Diagnostics;
using System.Text;

namespace NaiveMq.LoadTests.SpamQueue
{
    public class QueueSpamService : BackgroundService
    {
        private CancellationToken _stoppingToken;
        private ILogger<QueueSpamService> _logger;
        private IOptions<QueueSpamServiceOptions> _options;
        private readonly NaiveMqService _queueService;
        private readonly IServiceProvider _serviceProvider;

        public QueueSpamService(ILogger<QueueSpamService> logger, IServiceProvider serviceProvider, IOptions<QueueSpamServiceOptions> options, NaiveMqService queueService)
        {
            _logger = logger;
            _options = options;
            _queueService = queueService;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            if (_options.Value.IsEnabled)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_queueService.Online)
                    {
                        break;
                    }

                    await Task.Delay(1000, _stoppingToken);
                }

                using var timer = new Timer((s) =>
                {
                    _queueService.Storage.Users[_options.Value.Username].Queues.TryGetValue(_options.Value.QueueName + "1", out var queue);

                    _logger.LogInformation($"{DateTime.Now:O};Read message/s;{_queueService.Storage.ReadMessageCounter.LastResult};" +
                        $"Write message/s;{_queueService.Storage.WriteMessageCounter.LastResult};" +
                        $"Read/s;{_queueService.Storage.ReadCounter.LastResult};" +
                        $"Write/s;{_queueService.Storage.WriteCounter.LastResult};" +
                        $"Total read;{_queueService.Storage.ReadCounter.Total};" +
                        $"Total write;{_queueService.Storage.WriteCounter.Total};" +
                        $"QueueLength;{queue?.Length};" +
                        $"QueueVolume;{queue?.Volume};") ;
                }, null, 0, 1000);

                await QueueSpamAsync();
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, _stoppingToken);
            }
        }

        private Task OnMessageReceived(NaiveMqClient sender, ICommand args)
        {
            return Task.CompletedTask;
        }

        private async Task QueueSpamAsync()
        {
            var clientLogger = _serviceProvider.GetRequiredService<ILogger<NaiveMqClient>>();

            await Task.Run(async () =>
            {
                await Task.Delay(1000);
                
                var taskCount = _options.Value.ThreadsCount;
                var max = _options.Value.MessageCount;

                var options = new NaiveMqClientOptions { Hosts = _options.Value.Hosts, Parallelism = _options.Value.Parallelism };

                using var c = new NaiveMqClient(options, clientLogger, _stoppingToken);

                // c.Start();

                if (!string.IsNullOrEmpty(_options.Value.Username))
                {
                    await c.SendAsync(new Login { Username = _options.Value.Username, Password = _options.Value.Password }, _stoppingToken);
                }

                for (var queue = 1; queue <= _options.Value.QueueCount; queue++)
                {
                    await CheckQueueCommandsAsync(c, queue);
                }

                await CheckUserCommandsAsync(c);

                await CheckExchangeAsync(c);

                var swt = Stopwatch.StartNew();

                var message = Encoding.UTF8.GetBytes(string.Join("", Enumerable.Range(0, _options.Value.MessageLength).Select(x => "*")));

                var consumers = new List<NaiveMqClient>();

                await CreateConsumersAsync(clientLogger, taskCount, options, consumers);

                for (var run = 0; run < _options.Value.Runs; run++)
                {
                    _logger.LogInformation($"Run {run + 1} is started.");
                    
                    RunProducers(clientLogger, taskCount, max, options, message);

                    _logger.LogInformation($"Run {run + 1} is ended. Sent {max * taskCount} messages in {swt.Elapsed}.");
                }

                await UnsubscribeAsync(c, consumers);

                for (var queue = 1; queue <= _options.Value.QueueCount; queue++)
                {
                    if (_options.Value.DeleteQueue)
                        await c.SendAsync(new DeleteQueue { Name = _options.Value.QueueName + queue }, _stoppingToken);
                }
            });
        }

        private async Task UnsubscribeAsync(NaiveMqClient c, List<NaiveMqClient> consumers)
        {
            if (_options.Value.Subscribe)
            {
                foreach (var consumer in consumers)
                {
                    for (var queue = 1; queue <= _options.Value.QueueCount; queue++)
                    {
                        var queueName = _options.Value.QueueName + queue;
                        await consumer.SendAsync(new Unsubscribe { Queue = queueName }, _stoppingToken);
                    }
                }
            }
        }

        private void RunProducers(ILogger<NaiveMqClient> clientLogger, int taskCount, int max, NaiveMqClientOptions options, byte[] message)
        {
            var tasks = new List<Task>();

            for (var queue = 1; queue <= _options.Value.QueueCount; queue++)
            {
                var queueName = _options.Value.QueueName + queue;

                for (var i = 0; i < taskCount; i++)
                {
                    var t = Task.Run(async () =>
                    {
                        using var c = new NaiveMqClient(options, clientLogger, _stoppingToken);

                        if (!string.IsNullOrEmpty(_options.Value.Username))
                        {
                            await c.SendAsync(new Login { Username = _options.Value.Username, Password = _options.Value.Password }, _stoppingToken);
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
            if (_options.Value.Subscribe)
            {
                var tasks = new List<Task>();

                for (var queue = 1; queue <= _options.Value.QueueCount; queue++)
                {
                    var queueName = _options.Value.QueueName + queue;

                    for (var i = 0; i < taskCount; i++)
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            var client = new NaiveMqClient(options, clientLogger, _stoppingToken);

                            if (!string.IsNullOrEmpty(_options.Value.Username))
                            {
                                await client.SendAsync(new Login { Username = _options.Value.Username, Password = _options.Value.Password }, _stoppingToken);
                            }

                            await client.SendAsync(new Subscribe { Queue = queueName, ConfirmMessage = _options.Value.ConfirmSubscription, ConfirmMessageTimeout = _options.Value.ConfirmMessageTimeout }, _stoppingToken);

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
                if (_options.Value.Batch)
                {
                    var batch = new Batch
                    {
                        Messages = Enumerable.Range(0, _options.Value.BatchSize).Select(x => CreateMessage(bytes, queueName)).ToList()
                    };

                    var response = await c.SendAsync(batch, _options.Value.Wait, _stoppingToken);
                }
                else
                {
                    var response = await c.SendAsync(CreateMessage(bytes, queueName), _options.Value.Wait, _stoppingToken);
                }

                if (_options.Value.SendDelay != null)
                {
                    await Task.Delay(_options.Value.SendDelay.Value);
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
                Persistent = _options.Value.PersistentMessage,
                Request = _options.Value.Request,
                Data = bytes,
                Confirm = _options.Value.Confirm,
                ConfirmTimeout = _options.Value.ConfirmTimeout,
            };
        }

        private void Consume(NaiveMqClient c)
        {
            c.OnReceiveMessageAsync += async (client, message) =>
            {
                if (_options.Value.ReadBody)
                {
                    var body = message.Data.ToArray();
                }

                if (message.Confirm)
                {
                    try
                    {
                        await client.SendAsync(Confirmation.Ok(message, message.Request ? Encoding.UTF8.GetBytes("Answer") : null), _stoppingToken);
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

                if (_options.Value.ReceiveDelay != null)
                {
                    await Task.Delay(_options.Value.ReceiveDelay.Value);
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
            if (_options.Value.AddExchange)
            {
                await c.SendAsync(new AddQueue { Name = _options.Value.Exchange, Durable = true, Exchange = true }, _stoppingToken);
                await c.SendAsync(new AddQueue { Name = _options.Value.ExchangeTo, Durable = true }, _stoppingToken);
            }

            if (_options.Value.AddBinding)
            {
                await c.SendAsync(new AddBinding { Exchange = _options.Value.Exchange, Queue = _options.Value.ExchangeTo, Durable = true, Pattern = _options.Value.BindingPattern }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.Value.SendExchangeMessageWithKey))
            {
                await c.SendAsync(new Message { Queue = _options.Value.Exchange, Confirm = true, Persistent = Persistence.Yes, RoutingKey = _options.Value.SendExchangeMessageWithKey, Data = Encoding.UTF8.GetBytes("Some text to exchange") }, _stoppingToken);
            }

            if (_options.Value.DeleteBinding)
            {
                await c.SendAsync(new DeleteBinding { Exchange = _options.Value.Exchange, Queue = _options.Value.ExchangeTo }, _stoppingToken);
            }
        }

        private async Task CheckQueueCommandsAsync(NaiveMqClient c, int queue)
        {
            var queueName = _options.Value.QueueName + queue;

            if (_options.Value.AddQueue)
            {
                if ((await c.SendAsync(new GetQueue { Name = queueName, Try = true }, _stoppingToken)).Entity != null)
                {
                    if (_options.Value.RewriteQueue)
                    {
                        await c.SendAsync(new DeleteQueue { Name = queueName }, _stoppingToken);
                    }
                }

                await c.SendAsync(new AddQueue
                {
                    Name = queueName,
                    Durable = _options.Value.Durable,
                    LengthLimit = _options.Value.LengthLimit,
                    VolumeLimit = _options.Value.VolumeLimit,
                    LimitStrategy = _options.Value.LimitStrategy,
                }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.Value.SearchQueues))
            {
                await c.SendAsync(new SearchQueues { Name = _options.Value.SearchQueues }, _stoppingToken);
            }

            if (_options.Value.ClearQueue)
            {
                await c.SendAsync(new ClearQueue { Name = queueName }, _stoppingToken);
            }
        }

        private async Task CheckUserCommandsAsync(NaiveMqClient c)
        {
            if (_options.Value.GetProfile)
            {
                await c.SendAsync(new GetProfile(), _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.Value.ChangePassword))
            {
                await c.SendAsync(new ChangePassword { CurrentPassword = _options.Value.Password, NewPassword = _options.Value.ChangePassword }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.Value.GetUser))
            {
                await c.SendAsync(new GetUser { Username = _options.Value.AddUser, Try = _options.Value.GetUserTry }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.Value.SearchUsers))
            {
                await c.SendAsync(new SearchUsers { Username = _options.Value.SearchUsers }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.Value.AddUser))
            {
                await c.SendAsync(new AddUser { Username = _options.Value.AddUser, Administrator = true, Password = "guest" }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.Value.UpdateUser))
            {
                await c.SendAsync(new UpdateUser { Username = _options.Value.UpdateUser, Administrator = true }, _stoppingToken);
            }

            if (_options.Value.DeleteUser)
            {
                await c.SendAsync(new DeleteUser { Username = _options.Value.AddUser }, _stoppingToken);
            }
        }
    }
}