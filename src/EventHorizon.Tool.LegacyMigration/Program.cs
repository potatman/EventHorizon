using System.Globalization;
using System.Threading.Tasks;
using Destructurama;
using EventHorizon.Abstractions.Extensions;
using EventHorizon.EventStore.MongoDb.Extensions;
using EventHorizon.EventStreaming.Pulsar.Extensions;
using EventHorizon.Tool.LegacyMigration.HostedServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EventHorizon.Tool.LegacyMigration;

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
            .UseSerilog((_, config) => { config
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .Destructure.UsingAttributes();
            })
            .Build()
            .RunAsync();
    }
}
