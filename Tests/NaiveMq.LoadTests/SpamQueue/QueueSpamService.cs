using Microsoft.Extensions.Options;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using System.Diagnostics;
using System.Text;

namespace NaiveMq.LoadTests.SpamQueue
{
    public class QueueSpamService : BackgroundService
    {
        private CancellationToken _stoppingToken;
        private ILogger<QueueSpamService> _logger;
        private IOptions<QueueSpamServiceOptions> _options;
        private readonly NaiveMq.Service.NaiveMqService _queueService;
        private readonly IServiceProvider _serviceProvider;

        public QueueSpamService(ILogger<QueueSpamService> logger, IServiceProvider serviceProvider, IOptions<QueueSpamServiceOptions> options, NaiveMq.Service.NaiveMqService queueService)
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
                    if (_queueService.IsLoaded)
                    {
                        break;
                    }

                    await Task.Delay(1000, _stoppingToken);
                }

                using var timer = new Timer((s) =>
                {
                    _logger.LogInformation($"{DateTime.Now:O};Read message/s;{_queueService.ReadMessageCounter.LastResult};Write message/s;{_queueService.WriteMessageCounter.LastResult};Read/s;{_queueService.ReadCounter.LastResult};Write/s;{_queueService.WriteCounter.LastResult};Total read;{_queueService.ReadCounter.Total};Total write;{_queueService.WriteCounter.Total}");
                }, null, 0, 1000);

                await QueueSpam();
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

        private async Task QueueSpam()
        {
            var clientLogger = _serviceProvider.GetRequiredService<ILogger<NaiveMqClient>>();

            await Task.Run(async () =>
            {
                await Task.Delay(1000);
                
                var taskCount = _options.Value.ThreadsCount;
                var max = _options.Value.MessageCount;

                var options = new NaiveMqClientOptions { Host = _options.Value.Host, Port = _options.Value.Port, Parallelism = _options.Value.Parallelism };

                using var c = new NaiveMqClient(options, clientLogger, _stoppingToken);

                // c.Start();

                if (!string.IsNullOrEmpty(_options.Value.Username))
                {
                    await c.SendAsync(new Login { Username = _options.Value.Username, Password = _options.Value.Password }, _stoppingToken);
                }

                await CheckQueueCommands(c);

                await CheckUserCommands(c);

                await CheckExchange(c);

                var swt = Stopwatch.StartNew();

                var message = Encoding.UTF8.GetBytes(string.Join("", Enumerable.Range(0, _options.Value.MessageLength).Select(x => "*")));

                for (var run = 0; run < _options.Value.Runs; run++)
                {
                    _logger.LogInformation($"Run {run + 1} is started.");

                    var tasks = new List<Task>();

                    for (var i = 0; i < taskCount; i++)
                    {
                        var poc = i;
                        var t = Task.Run(async () =>
                        {
                            using var c = new NaiveMqClient(options, clientLogger, _stoppingToken);

                            // c.Start();

                            c.OnReceiveMessageAsync += async (client, message) =>
                            {
                                if (message.Confirm || message.Request)
                                {
                                    try
                                    {
                                        await client.SendAsync(Confirmation.Ok(message.Id, "Answer"), _stoppingToken);
                                    }
                                    catch (ClientException)
                                    {
                                        // it's ok
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Send errr");
                                    }
                                }

                                if (_options.Value.ReceiveDelay != null)
                                {
                                    await Task.Delay(_options.Value.ReceiveDelay.Value);
                                }
                            };

                            c.OnReceiveErrorAsync += (client, ex) =>
                            {
                                _logger.LogError(ex, "Spam receive error.");

                                return Task.CompletedTask;
                            };

                            if (!string.IsNullOrEmpty(_options.Value.Username))
                            {
                                await c.SendAsync(new Login { Username = _options.Value.Username, Password = _options.Value.Password }, _stoppingToken);
                            }

                            if (_options.Value.Subscribe)
                            {
                                await c.SendAsync(new Subscribe { Queue = _options.Value.QueueName, ConfirmMessage = _options.Value.ConfirmSubscription, ConfirmMessageTimeout = _options.Value.ConfirmMessageTimeout }, _stoppingToken);
                            }

                            using var exitSp = new SemaphoreSlim(1, 1);

                            var sw = Stopwatch.StartNew();
                            await exitSp.WaitAsync();

                            var lastActivity = DateTime.Now;
                            TimeSpan delta = TimeSpan.Zero;

                            using var timer = new Timer((s) =>
                            {
                                if (_options.Value.LogClientCounters)
                                {
                                    _logger.LogInformation($"Client {c.Id} speed: read {c.ReadCounter.LastResult}, write {c.WriteCounter.LastResult}");
                                }

                                if (c.ReadCounter.LastResult > 0 || c.WriteCounter.LastResult > 0)
                                {
                                    lastActivity = DateTime.Now;
                                }
                                else
                                {
                                    if (DateTime.Now.Subtract(lastActivity).TotalSeconds > 5)
                                    {
                                        delta = DateTime.Now.Subtract(lastActivity);
                                        if (exitSp.CurrentCount == 0)
                                            exitSp.Release(1);
                                    }
                                }
                            }, null, 0, 1000);

                            for (var j = 1; j <= max; j++)
                            {
                                try
                                {
                                    var response = await c.SendAsync(new Message
                                        {
                                            Queue = _options.Value.QueueName,
                                            Durable = _options.Value.DurableMessage,
                                            Request = _options.Value.Request,
                                            Data = message,
                                            Confirm = _options.Value.Confirm,
                                            ConfirmTimeout = _options.Value.ConfirmTimeout,
                                        },
                                        _stoppingToken);


                                    if (_options.Value.SendDelay != null)
                                    {
                                        await Task.Delay(_options.Value.SendDelay.Value);
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    if ((ex as ClientException)?.ErrorCode != ErrorCode.ClientStopped)
                                    {
                                        _logger.LogError(ex, "Spam send error.");
                                    }
                                    throw;
                                }
                            }

                            await exitSp.WaitAsync();

                            timer.Dispose();

                            if (_options.Value.Subscribe)
                            {
                                await c.SendAsync(new Unsubscribe { Queue = _options.Value.QueueName }, _stoppingToken);
                            }

                            _logger.LogInformation($"Client {c.Id} took {sw.Elapsed.Subtract(delta)} to finish. Read: {c.ReadCounter.Total}, write {c.WriteCounter.Total}");
                        });

                        tasks.Add(t);
                    }

                    try
                    {
                        Task.WaitAll(tasks.ToArray());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Spam not all ended well: {ex.GetBaseException().Message}");
                    }

                    _logger.LogInformation($"Run {run + 1} is ended. Sent {max * taskCount} messages in {swt.Elapsed}.");
                }

                if (_options.Value.DeleteQueue)
                    await c.SendAsync(new DeleteQueue { Name = _options.Value.QueueName }, _stoppingToken);
            });
        }

        private async Task CheckExchange(NaiveMqClient c)
        {
            if (_options.Value.AddExchange)
            {
                await c.SendAsync(new AddQueue { Name = _options.Value.Exchange, Durable = true, Exchange = true }, _stoppingToken);
                await c.SendAsync(new AddQueue { Name = _options.Value.ExchangeTo, Durable = true }, _stoppingToken);
            }

            if (_options.Value.AddBinding)
            {
                await c.SendAsync(new AddBinding { Exchange = _options.Value.Exchange, Queue = _options.Value.ExchangeTo, Durable = true, Regex = _options.Value.BindingRegex }, _stoppingToken);
            }

            if (!string.IsNullOrEmpty(_options.Value.SendExchangeMessageWithKey))
            {
                await c.SendAsync(new Message { Queue = _options.Value.Exchange, Confirm = true, Durable = true, BindingKey = _options.Value.SendExchangeMessageWithKey, Data = Encoding.UTF8.GetBytes("Some text to exchange") }, _stoppingToken);
            }

            if (_options.Value.DeleteBinding)
            {
                await c.SendAsync(new DeleteBinding { Exchange = _options.Value.Exchange, Queue = _options.Value.ExchangeTo }, _stoppingToken);
            }
        }

        private async Task CheckQueueCommands(NaiveMqClient c)
        {
            if (_options.Value.AddQueue)
            {
                if ((await c.SendAsync(new GetQueue { Name = _options.Value.QueueName, Try = true }, _stoppingToken)).Queue != null)
                {
                    if (_options.Value.RewriteQueue)
                    {
                        await c.SendAsync(new DeleteQueue { Name = _options.Value.QueueName }, _stoppingToken);
                        await c.SendAsync(new AddQueue { Name = _options.Value.QueueName, Durable = _options.Value.Durable }, _stoppingToken);
                    }
                }
                else
                {
                    await c.SendAsync(new AddQueue { Name = _options.Value.QueueName, Durable = _options.Value.Durable }, _stoppingToken);
                }
            }

            if (!string.IsNullOrEmpty(_options.Value.SearchQueues))
            {
                await c.SendAsync(new SearchQueues { Name = _options.Value.SearchQueues }, _stoppingToken);
            }
        }

        private async Task CheckUserCommands(NaiveMqClient c)
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