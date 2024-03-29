﻿using Microsoft.Extensions.Logging;
using NaiveMq.Client;
using NaiveMq.Client.Cogs;
using NaiveMq.Client.Commands;
using NaiveMq.Client.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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

var jsonSettings = new JsonSerializerSettings
{
    ContractResolver = new JsonOriginalPropertiesResolver(),
    Converters = new List<JsonConverter> { new StringEnumConverter() }
};

var client = CreateClient();

Console.WriteLine(@"NaiveMq management console.
type:
'connect host:port' for connect to NaiveMq server or 'connect' to connect to localhost:8506,
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

        var found = Connect(input);
        found = found || Close(input);
        found = found || Commands(input);
        found = found || await SendAsync(input);

        if (!found)
        {
            throw new Exception("Command not found.");
        }
    }
    catch (Exception ex)
    {
        using var cc = new ConsoleContext(errorColor);

        Console.WriteLine("Error on processing command: " + ex.Message);
    }
} while (!quit);

void WriteCommand(bool isOut, ICommand command)
{
    var dataCommand = command as IDataCommand;
    Console.WriteLine($"{(isOut ? "Out" : "In")} {command.GetType().Name} {JsonConvert.SerializeObject(command, jsonSettings)}" +
        $"{(dataCommand != null ? $", DataLength: {dataCommand.Data.Length}" : string.Empty)}");
}

NaiveMqClient CreateClient()
{
    var client = new NaiveMqClient(new NaiveMqClientOptions { AutoStart = false, AutoRestart = false }, logger, CancellationToken.None);

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
        WriteCommand(false, command);
        return Task.CompletedTask;
    };

    client.OnSendCommandAsync += (sender, command) =>
    {
        WriteCommand(true, command);
        return Task.CompletedTask;
    };

    return client;
}

static bool Equals(string input, string other)
{
    return input.Equals(other, StringComparison.InvariantCultureIgnoreCase);
}

static string FriendlyTypeName(Type type)
{
    if (type.IsGenericType)
    {
        var namePrefix = type.Name.Split(new[] { '`' }, StringSplitOptions.RemoveEmptyEntries)[0];
        var genericParameters = type.GetGenericArguments().Select(FriendlyTypeName);
        return namePrefix + "<" + string.Join(",", genericParameters) + ">";
    }

    return type.Name;
}

bool Connect(string input)
{
    if (input.StartsWith("connect", StringComparison.InvariantCultureIgnoreCase))
    {
        var startSplit = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        if (startSplit.Length > 1)
        {
            client.Options.Hosts = startSplit[1];
        }

        client.Start(false);

        return true;
    }

    return false;
}

bool Close(string input)
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

        foreach (var commandType in NaiveMqClient.Commands.Where(x => x.GetInterface(nameof(IRequest)) != null).OrderBy(x => x.Name))
        {
            var command = Activator.CreateInstance(commandType);

            if (command != null)
            {
                var props = string.Join(", ", command.GetType().GetProperties().OrderBy(x => x.Name).Select(x => $"{x.Name}:\"{FriendlyTypeName(x.PropertyType)}\""));
                Console.WriteLine($"{commandType.Name} {props}");
            }
        }

        Console.WriteLine("---------");
        Console.WriteLine("Parameters note: 'Id', 'Confirm', 'ConfirmTimeout' are optional. Values are JSON formatted.");
        Console.WriteLine("---------");
        Console.WriteLine("Enumerables are:");

        foreach (var enumType in new[] { typeof(ClusterStrategy), typeof(LimitStrategy), typeof(Persistence), typeof(QueueStatus) })
        {
            var values = string.Join(", ", Enum.GetNames(enumType));
            Console.WriteLine($"{enumType.Name}: {values}");
        }

        return true;
    }

    return false;
}

async Task<bool> SendAsync(string input)
{
    var split = input.Split(' ', 2);

    var func = client.GetType().GetMethods().First(x => x.Name == nameof(client.SendAsync) && x.GetParameters().Length == 4);

    if (split.Length > 0 && NaiveMqClient.Commands.TryGetValue(split[0], out var commandType))
    {
        var command = JsonConvert.DeserializeObject(
            $"{{ {(split.Length > 1 ? split[1] : string.Empty)} }}",
            commandType, 
            jsonSettings);

        if (command != null && commandType.BaseType != null)
        {
            var method = func.MakeGenericMethod(commandType.BaseType.GetGenericArguments());
            await (Task)method.Invoke(client, new object[] { command, true, true, CancellationToken.None });

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