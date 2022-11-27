using Microsoft.Extensions.Options;
using RabbitMQ.Client;
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
        private HttpClient _httpClient;

        public RabbitSpamService(ILogger<RabbitSpamService> logger, IOptions<RabbitSpamServiceOptions> options, IOptions<RabbitOptions> rabbitOptions, HttpClient httpClient)
        {
            _logger = logger;
            _options = options;
            _httpClient = httpClient;
            _rabbitOptions = rabbitOptions;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;

            await WebSpam();

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
                channel.QueueDelete("hello");

                channel.QueueDeclare(queue: "hello",
                                     durable: _options.Value.Durable,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);
            }

            var tasks = new List<Task>();

            string message = string.Join("", Enumerable.Range(0, _options.Value.MessageLength).Select(x => x));

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

                    }

                    for (var j = 1; j <= _options.Value.MessageCount; j++)
                    {
                        var body = Encoding.UTF8.GetBytes($"{message} {poc} says {j}.");

                        var props = channel.CreateBasicProperties();
                        props.Persistent = _options.Value.Durable; // or props.DeliveryMode = 2;

                        channel.BasicPublish(exchange: "",
                                             routingKey: "hello",
                                             basicProperties: props,
                                             body: body);

                        if (_options.Value.Confirm)
                            channel.WaitForConfirms();
                    }

                    return Task.CompletedTask;
                });

                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());

            Console.WriteLine($"Took {sw.Elapsed}");
            Console.ReadLine();

            return Task.CompletedTask;
        }
    }
}