using NaiveMq.LoadTests.SpamQueue;
using NaiveMq.Service;
using NaiveMq.Service.PersistentStorage;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services.Configure<RabbitOptions>(configuration.GetSection("Rabbit"));
        services.Configure<NaiveMqServiceOptions>(configuration.GetSection("Queue"));
        services.Configure<FilePersistentStorageOptions>(configuration.GetSection("FilePersistentStorage"));
        services.Configure<QueueSpamServiceOptions>(configuration.GetSection("QueueSpam"));
        services.Configure<RabbitSpamServiceOptions>(configuration.GetSection("RabbitSpam"));

        services.AddSingleton<IPersistentStorage, FilePersistentStorage>();
        services.AddHostedService((sp) => sp.GetRequiredService<NaiveMqService>());
        services.AddSingleton<NaiveMqService>(); 
        services.AddHostedService<QueueSpamService>();
        services.AddHostedService<RabbitSpamService>();
    })
    .Build();

await host.RunAsync();
