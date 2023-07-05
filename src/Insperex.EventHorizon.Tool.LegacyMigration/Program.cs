using System.Globalization;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.EventStore.MongoDb.Extensions;
using Insperex.EventHorizon.EventStreaming.Pulsar.Extensions;
using Insperex.EventHorizon.Tool.LegacyMigration.HostedServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Insperex.EventHorizon.Tool.LegacyMigration;

public class Program
{
    static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddEventHorizon(x =>
                {
                    x.AddPulsarEventStream(hostContext.Configuration.GetSection("Pulsar").Bind);
                    x.AddMongoDbSnapshotStore(hostContext.Configuration.GetSection("MongoDb").Bind);
                    // x.AddSubscription<NullStreamConsumer, Event>(s =>
                    //     s.AddStream<FileEntryEvent>()
                    //         .BatchSize(10000)
                    //         .SubscriptionName("test-2"));
                });

                // Runs Migration
                services.AddHostedService<MigrationHostedService>();
            })
            .UseConsul()
            .UseSerilog((_, config) => { config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture); })
            .Build()
            .RunAsync();
    }
}
