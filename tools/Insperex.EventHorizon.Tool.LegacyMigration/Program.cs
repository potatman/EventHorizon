using System.Globalization;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.EventStore.MongoDb.Extensions;
using Insperex.EventHorizon.EventStreaming.Pulsar.Extensions;
using Insperex.EventHorizon.Tool.LegacyMigration.HostedServices;
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
                // Runs Migration
                services.AddHostedService<MigrationHostedService>();

                services.AddEventHorizon(hostContext.Configuration, x =>
                {
                    x.AddMongoDbSnapshotStore()
                        // .AddInMemoryEventStream()
                        .AddPulsarEventStream();
                });
            })
            .UseSerilog((_, config) => { config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture); })
            .Build()
            .RunAsync();
    }
}
