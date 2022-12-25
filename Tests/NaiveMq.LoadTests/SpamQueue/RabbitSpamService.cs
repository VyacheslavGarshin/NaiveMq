using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace NaiveMq.LoadTests.SpamQueue
{
    public class RabbitSpamService : BackgroundService
    {
        private CancellationToken _stoppingToken;
        private ILogger<RabbitSpamService> _logger;
        private IOptions<RabbitSpamServiceOptions> _options;
        private IOptions<RabbitOptions> _rabbitOptions;

        public RabbitSpamService(ILogger<RabbitSpamService> logger, IOptions<RabbitSpamServiceOptions> options, IOptions<RabbitOptions> rabbitOptions)
        {
            _logger = logger;
            _options = options;
            _rabbitOptions = rabbitOptions;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            if (_options.Value.IsEnabled)
            {
                await WebSpam();
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, _stoppingToken);
            }
        }

        private Task WebSpam()
        {
            var sw = Stopwatch.StartNew();

            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                ContinuationTimeout = TimeSpan.FromMinutes(3),
                HandshakeContinuationTimeout = TimeSpan.FromMinutes(3),
                RequestedConnectionTimeout = TimeSpan.FromMinutes(3)
            };

            using var connection = factory.CreateConnection();

            using var channel = connection.CreateModel();

            CreateQueues(channel);

            string message = string.Join("", Enumerable.Range(0, _options.Value.MessageLength).Select(x => "*"));
            var body = Encoding.UTF8.GetBytes(message);

            for (var run = 0; run < _options.Value.Runs; run++)
            {
                _logger.LogInformation($"Run {run + 1} is started.");

                var tasks = new List<Task>();

                for (var queue = 1; queue <= _options.Value.QueueCount; queue++)
                {
                    for (var i = 0; i < _options.Value.ThreadsCount; i++)
                    {
                        var queueName = _options.Value.QueueName + queue;

                        var t = Task.Run(() =>
                        {
                            try
                            {
                                // using var connection = factory.CreateConnection();
                                using var channel = connection.CreateModel();

                                Consume(queueName, channel);

                                if (_options.Value.Confirm)
                                    channel.ConfirmSelect();

                                var number = 1;

                                for (var j = 1; j <= _options.Value.MessageCount; j++)
                                {
                                    Publish(body, queueName, channel, number);

                                    if (number < _options.Value.BatchSize)
                                    {
                                        number++;
                                    }
                                    else
                                    {
                                        number = 1;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error in spam task.");
                            }

                            return Task.CompletedTask;
                        });

                        tasks.Add(t);
                    }
                }

                Task.WaitAll(tasks.ToArray());

                Console.WriteLine($"Took {sw.Elapsed}");
                Console.ReadLine();
            }

            return Task.CompletedTask;
        }

        private void CreateQueues(IModel channel)
        {
            for (var queue = 1; queue <= _options.Value.QueueCount; queue++)
            {
                {
                    channel.QueueDelete(_options.Value.QueueName + queue);

                    channel.QueueDeclare(queue: _options.Value.QueueName + queue,
                                         durable: _options.Value.Durable,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null);
                }
            }
        }

        private void Publish(byte[] body, string queueName, IModel channel, int number)
        {
            var props = channel.CreateBasicProperties();
            props.Persistent = _options.Value.Durable; // or props.DeliveryMode = 2;

            try
            {
                channel.BasicPublish(exchange: "",
                                     routingKey: queueName,
                                     basicProperties: props,
                                     body: body);

                if (_options.Value.Confirm && (number == _options.Value.BatchSize || !_options.Value.Batch))
                    channel.WaitForConfirms();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Basic publish failed.");
                throw;
            }
        }

        private void Consume(string queueName, IModel channel)
        {
            if (_options.Value.Subscribe)
            {
                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    if (_options.Value.ReadBody)
                    {
                        var body = ea.Body.ToArray();
                    }

                    if (!_options.Value.AutoAck)
                    {
                        channel.BasicAck(ea.DeliveryTag, false);
                    }
                };
                channel.BasicConsume(queue: queueName,
                                     autoAck: _options.Value.AutoAck,
                                     consumer: consumer);
            }
        }
    }
}