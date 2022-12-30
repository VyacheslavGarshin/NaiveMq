using FluentAssertions;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Common;
using NaiveMq.Client.Dto;
using NaiveMq.Client.Serializers;
using NaiveMq.Service;
using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace NaiveMq.Client.UnitTests
{
    public class NaiveCommandSerializerUnitTests
    {
        private NaiveCommandSerializer _serializer;
        private JsonCommandSerializer _jSerializer;
        private CommandPacker _commandPacker;

        static NaiveCommandSerializerUnitTests()
        {
            NaiveMqClient.RegisterCommands(typeof(NaiveMqService).Assembly);
        }

        [SetUp]
        public void Setup()
        {
            _serializer = new();
            _jSerializer = new();
            _commandPacker = new(_serializer, ArrayPool<byte>.Shared);
        }

        [Test]
        public void SerializeCommands()
        {
            foreach (var type in NaiveMqClient.Commands.Values)
            {
                Console.Write(type.FullName);

                var command = PrepareCommand(Activator.CreateInstance(type) as ICommand);
                command.Prepare(_commandPacker);

                var act = () => { return _serializer.Serialize(command); };

                var bytes = act.Should().NotThrow().Which;
                bytes.Should().NotBeEmpty();

                var actD = () => { return _serializer.Deserialize(bytes, command.GetType()); };

                var commandD = actD.Should().NotThrow().Which;

                var properties = NaiveCommandSerializer.TypeDefinitions[type];

                Console.WriteLine(", done");

                foreach (var property in properties.Values)
                {
                    var value = property.PropertyInfo.GetValue(command);
                    var valueD = property.PropertyInfo.GetValue(commandD);

                    value.Should().BeEquivalentTo(valueD);
                }
            }
        }

        [Test]
        public void CommandSpeed()
        {
            var command = PrepareCommand(new Message());
            command.Prepare(_commandPacker);
            var type = command.GetType();

            var count = 10000;

            var sw = Stopwatch.StartNew();
            var result = SerializeNaive(command, count);
            sw.Stop();
            var time = sw.ElapsedTicks;

            sw.Restart();
            for (var i = 0; i < count; i++)
            {
                var tuple = _serializer.Serialize(command, ArrayPool<byte>.Shared);
                ArrayPool<byte>.Shared.Return(tuple.Buffer);
            }
            sw.Stop();
            var timeS = sw.ElapsedTicks;

            sw.Restart();
            var jResult = SerializeJson(command, count);
            sw.Stop();
            var jTime = sw.ElapsedTicks;

            Console.WriteLine($"Serialize: {time} ticks");
            Console.WriteLine($"Serialize pool: {timeS} ticks");
            Console.WriteLine($"Json Serialize: {jTime} ticks");
            Console.WriteLine($"Serialize: {result?.Length} bytes, Json Serialize: {jResult?.Length} bytes.");
            Console.WriteLine($"Serialize: {Encoding.ASCII.GetString(result)} bytes.");
            Console.WriteLine($"Json Serialize: {Encoding.UTF8.GetString(jResult)} bytes.");
            time.Should().BeLessThan(jTime);

            sw.Restart();
            for (var i = 0; i < count; i++)
            {
                _serializer.Deserialize(new ReadOnlyMemory<byte>(result), type);
            }
            sw.Stop();
            time = sw.ElapsedTicks;

            sw.Restart();
            for (var i = 0; i < count; i++)
            {
                _jSerializer.Deserialize(new ReadOnlyMemory<byte>(jResult), type);
            }
            sw.Stop();
            jTime = sw.ElapsedTicks;

            Console.WriteLine($"Deserialize: {time} ticks");
            Console.WriteLine($"Json Deserialize: {jTime} ticks.");
            time.Should().BeLessThan(jTime);
        }

        private byte[] SerializeJson(ICommand command, int count)
        {
            byte[] result = null;

            for (var i = 0; i < count; i++)
            {
                result = _jSerializer.Serialize(command);
            }

            return result;
        }

        private byte[] SerializeNaive(ICommand command, int count)
        {
            byte[] result = null;

            for (var i = 0; i < count; i++)
            {
                result = _serializer.Serialize(command);
            }

            return result;
        }

        [TestCase(10)]
        [TestCase(100)]
        public async Task CommandSpeedParallel(int threads)
        {
            var command = PrepareCommand(new Login());
            command.Prepare(_commandPacker);

            var count = 10000;
            var taks = new List<Task>();

            var sw = Stopwatch.StartNew();

            for (var i = 0; i < threads; i++)
            {
                taks.Add(Task.Run(() => { SerializeNaive(command, count); }));
            }

            await Task.WhenAll(taks);
            taks.Clear();
            sw.Stop();
            var time = sw.ElapsedTicks;

            sw.Restart();
            for (var i = 0; i < threads; i++)
            {
                taks.Add(Task.Run(() =>
                {
                    for (var i = 0; i < count; i++)
                    {
                        var tuple = _serializer.Serialize(command, ArrayPool<byte>.Shared);
                        ArrayPool<byte>.Shared.Return(tuple.Buffer);
                    }
                }));
            }

            await Task.WhenAll(taks);
            taks.Clear();
            sw.Stop();            
            var timeS = sw.ElapsedTicks;

            sw.Restart();
            for (var i = 0; i < threads; i++)
            {
                taks.Add(Task.Run(() => { SerializeJson(command, count); }));
            }

            await Task.WhenAll(taks);
            taks.Clear();
            sw.Stop();
            var jTime = sw.ElapsedTicks;

            Console.WriteLine($"Serialize: {time} ticks");
            Console.WriteLine($"Serialize pool: {timeS} ticks");
            Console.WriteLine($"Json Serialize: {jTime} ticks.");
            time.Should().BeLessThan(jTime);
        }

        private static ICommand PrepareCommand(ICommand command)
        {
            if (command is IRequest request)
            {
                request.ConfirmTimeout = TimeSpan.FromSeconds(1);
                request.Tag = "Tag";
            }

            if (command is Login login)
            {
                login.Username = "Username";
                login.Password = "Password";
            }
            else if (command is Message message)
            {
                message.RoutingKey = string.Empty;
                message.Data = new byte[100];
                message.Persistent = Enums.Persistence.Yes;
            }
            else if (command is AddQueue addQueue)
            {
                addQueue.LengthLimit = 10;
            }
            else if (command is GetQueueResponse getQueueResponse)
            {
                getQueueResponse.Entity = new Queue { Name = "test" };
            }
            else if (command is SearchQueuesResponse searchQueuesResponse)
            {
                searchQueuesResponse.Entities = new List<Queue> { new Queue { Name = "test" }, new Queue { Name = "test2" } };
            }

            return command;
        }
    }
}