using NaiveMq.Service;
using NaiveMq.Service.PersistentStorage;

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services.Configure<NaiveMqServiceOptions>(configuration.GetSection("NaiveMqService"));
        services.Configure<FilePersistentStorageOptions>(configuration.GetSection("FilePersistentStorage"));

        services.AddSingleton<IPersistentStorage, FilePersistentStorage>();
        services.AddHostedService((sp) => sp.GetRequiredService<NaiveMqService>());
        services.AddSingleton<NaiveMqService>(); 
    })
    .Build();

await host.RunAsync();
