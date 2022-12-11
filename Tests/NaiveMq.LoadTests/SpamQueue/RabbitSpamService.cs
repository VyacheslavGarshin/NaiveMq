using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;

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

            var factory = new ConnectionFactory() { HostName = "localhost" };

            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDelete(_options.Value.QueueName);

                channel.QueueDeclare(queue: _options.Value.QueueName,
                                     durable: _options.Value.Durable,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);
            }

            string message = string.Join("", Enumerable.Range(0, _options.Value.MessageLength).Select(x => "*"));
            var body = Encoding.UTF8.GetBytes(message);

            for (var run = 0; run < _options.Value.Runs; run++)
            {
                _logger.LogInformation($"Run {run + 1} is started.");

                var tasks = new List<Task>();

                for (var i = 0; i < _options.Value.ThreadsCount; i++)
                {
                    var poc = i;
                    var t = Task.Run(() =>
                    {
                        using var connection = factory.CreateConnection();
                        using var channel = connection.CreateModel();

                        if (_options.Value.Confirm)
                            channel.ConfirmSelect();

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
                            channel.BasicConsume(queue: _options.Value.QueueName,
                                                 autoAck: _options.Value.AutoAck,
                                                 consumer: consumer);
                        }

                        for (var j = 1; j <= _options.Value.MessageCount; j++)
                        {
                            var props = channel.CreateBasicProperties();
                            props.Persistent = _options.Value.Durable; // or props.DeliveryMode = 2;

                            try
                            {
                                channel.BasicPublish(exchange: "",
                                                     routingKey: _options.Value.QueueName,
                                                     basicProperties: props,
                                                     body: body);

                                if (_options.Value.Confirm)
                                    channel.WaitForConfirms();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Basic publish failed.");
                                throw;
                            }
                        }

                        return Task.CompletedTask;
                    });

                    tasks.Add(t);
                }

                Task.WaitAll(tasks.ToArray());

                Console.WriteLine($"Took {sw.Elapsed}");
                Console.ReadLine();
            }

            return Task.CompletedTask;
        }
    }
}