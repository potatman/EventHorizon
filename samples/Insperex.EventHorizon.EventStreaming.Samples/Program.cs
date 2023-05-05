using System.Globalization;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.InMemory.Extensions;
using Insperex.EventHorizon.EventStreaming.Pulsar.Extensions;
using Insperex.EventHorizon.EventStreaming.Samples.Handlers;
using Insperex.EventHorizon.EventStreaming.Samples.HostedServices;
using Insperex.EventHorizon.EventStreaming.Samples.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Insperex.EventHorizon.EventStreaming.Samples;

public class Program
{
    static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                // Feeds that Generate Data
                services.AddHostedService<Feed1HostedService>();
                services.AddHostedService<Feed2HostedService>();

                services.AddEventHorizon(x =>
                {
                    // Add Stream
                    // x.AddInMemoryEventStream();
                    x.AddPulsarEventStream(hostContext.Configuration);

                    // Add Hosted Subscription
                    x.AddSubscription<PriceChangeTracker, Event>(h =>
                    {
                        h.AddStream<Feed1PriceChanged>();
                        h.AddStream<Feed2PriceChanged>();
                    });
                });

            })
            .UseSerilog((_, config) => { config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture); })
            .UseEnvironment("local")
            .Build()
            .RunAsync();
    }
}
