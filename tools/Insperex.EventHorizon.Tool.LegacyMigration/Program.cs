using System.Globalization;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStore.MongoDb.Extensions;
using Insperex.EventHorizon.EventStreaming.Pulsar.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Insperex.EventHorizon.Tool.LegacyMigration;

class Program
{
    static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                // Runs Migration
                services.AddHostedService<MigrationHostedService>();

                // Add Stream
                // services.AddInMemoryEventStream();
                services.AddMongoDbSnapshotStore(hostContext.Configuration);
                services.AddPulsarEventStream(hostContext.Configuration);
            })
            .UseSerilog((_, config) => { config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture); })
            .Build()
            .RunAsync();
    }
}
