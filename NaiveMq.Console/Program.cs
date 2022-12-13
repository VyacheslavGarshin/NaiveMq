using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Enums;
using Newtonsoft.Json;
using System.Data;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)
        .AddFilter("NaiveMq.Client.NaiveMqClient", LogLevel.Warning)
        .AddConsole();
});
var logger = loggerFactory.CreateLogger<NaiveMqClient>();

var client = CreateClient(logger);

Console.WriteLine(@"NaiveMq management console.
type:
'connect host:port' for connect to NaiveMq server or 'start' to connect to localhost:8506,
'close' to disconnect,
'commands' for a list of commands,
'q' for quit
");

const ConsoleColor responseColor = ConsoleColor.DarkGray;
const ConsoleColor errorColor = ConsoleColor.DarkRed;

var quit = false;

do
{
    try
    {
        Console.Write(">");
        var input = Console.ReadLine();

        if (input == null)
        {
            continue;
        }

        input = input.Trim();

        if (new[] { "exit", "quit", "q" }.Contains(input, StringComparer.InvariantCultureIgnoreCase))
        {
            quit = true;
        }

        var found = Connect(client, input);
        found = found || Close(client, input);
        found = found || Commands(input);
        found = found || await SendAsync(client, input);

        if (!found)
        {
            throw new Exception("Command not found.");
        }
    }
    catch (Exception ex)
    {
        using (var cc = new ConsoleContext(errorColor))
        {
            Console.WriteLine("Error on processing command: " + ex.GetBaseException().Message);
        }
    }
} while (!quit);

static NaiveMqClient CreateClient(ILogger<NaiveMqClient> logger)
{
    var client = new NaiveMqClient(new NaiveMqClientOptions { Autostart = false }, logger, CancellationToken.None);

    client.OnStart += (sender) =>
    {
        using var сс = new ConsoleContext(responseColor);
        Console.WriteLine($"Client started.");
    };

    client.OnStop += (sender) =>
    {
        using var сс = new ConsoleContext(responseColor);
        Console.WriteLine("Client stopped.");
    };

    client.OnReceiveCommandAsync += (sender, command) =>
    {
        using var сс = new ConsoleContext(responseColor);
        Console.WriteLine($"In {command.GetType().Name} {JsonConvert.SerializeObject(command)}");
        return Task.CompletedTask;
    };

    client.OnSendCommandAsync += (sender, command) =>
    {
        Console.WriteLine($"Out {command.GetType().Name} {JsonConvert.SerializeObject(command)}");
        return Task.CompletedTask;
    };

    return client;
}

static bool Equals(string input, string other)
{
    return input.Equals(other, StringComparison.InvariantCultureIgnoreCase);
}

static string FriendlyName(Type type)
{
    if (type.IsGenericType)
    {
        var namePrefix = type.Name.Split(new[] { '`' }, StringSplitOptions.RemoveEmptyEntries)[0];
        var genericParameters = type.GetGenericArguments().Select(FriendlyName);
        return namePrefix + "<" + string.Join(",", genericParameters) + ">";
    }

    return type.Name;
}

static bool Connect(NaiveMqClient client, string input)
{
    if (input.StartsWith("connect", StringComparison.InvariantCultureIgnoreCase))
    {
        var startSplit = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        if (startSplit.Length > 1)
        {
            var destination = startSplit[1].Split(':', 2, StringSplitOptions.RemoveEmptyEntries);

            client.Options.Host = destination.First();
            client.Options.Port = int.Parse(destination.Last());
        }

        client.Start();

        return true;
    }

    return false;
}

static bool Close(NaiveMqClient client, string input)
{
    if (Equals(input, "close"))
    {
        client.Stop();

        return true;
    }

    return false;
}

static bool Commands(string input)
{
    if (Equals(input, "commands"))
    {
        using var сс = new ConsoleContext(responseColor);

        foreach (var commandType in NaiveMqClient.CommandTypes.Values.Where(x => x.GetInterface(nameof(IRequest)) != null).OrderBy(x => x.Name))
        {
            var command = Activator.CreateInstance(commandType);

            if (command != null)
            {
                var props = string.Join(", ", command.GetType().GetProperties().OrderBy(x => x.Name).Select(x => $"{x.Name}:\"{FriendlyName(x.PropertyType)}\""));
                Console.WriteLine($"{commandType.Name} {props}");
            }
        }

        Console.WriteLine("---------");
        Console.WriteLine("Parameters note: 'Id', 'Confirm', 'ConfirmTimeout' are optional.");
        Console.WriteLine("---------");
        Console.WriteLine("Enumerables are:");

        foreach (var enumType in new[] { typeof(LimitBy), typeof(LimitStrategy), typeof(Persistent) })
        {
            var values = string.Join(", ", Enum.GetNames(enumType));
            Console.WriteLine($"{enumType.Name}: {values}");
        }

        return true;
    }

    return false;
}

static async Task<bool> SendAsync(NaiveMqClient client, string input)
{
    var split = input.Split(' ', 2);

    var func = client.GetType().GetMethods().First(x => x.Name == nameof(client.SendAsync));

    if (split.Length > 0 && NaiveMqClient.CommandTypes.TryGetValue(split[0], out var commandType))
    {
        var command = JsonConvert.DeserializeObject($"{{ {(split.Length > 1 ? split[1] : string.Empty)} }}", commandType);

        if (command != null && commandType.BaseType != null)
        {
            var method = func.MakeGenericMethod(commandType.BaseType.GetGenericArguments());
            await (Task)method.Invoke(client, new object[] { command, CancellationToken.None });

            return true;
        }
    }

    return false;
}

class ConsoleContext : IDisposable
{
    public ConsoleContext(ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null)
    {
        if (foregroundColor != null)
        {
            Console.ForegroundColor = foregroundColor.Value;
        }

        if (backgroundColor != null)
        {
            Console.BackgroundColor = backgroundColor.Value;
        }
    }

    public void Dispose()
    {
        Console.ResetColor();
    }
}
