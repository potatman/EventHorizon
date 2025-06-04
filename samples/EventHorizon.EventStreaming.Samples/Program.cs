using System.Globalization;
using System.Threading.Tasks;
using Destructurama;
using EventHorizon.Abstractions.Extensions;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.InMemory.Extensions;
using EventHorizon.EventStreaming.Pulsar.Extensions;
using EventHorizon.EventStreaming.Samples.Handlers;
using EventHorizon.EventStreaming.Samples.HostedServices;
using EventHorizon.EventStreaming.Samples.Models;
using EventHorizon.EventStreaming.Subscriptions.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EventHorizon.EventStreaming.Samples;

public class Program
{
    static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Feeds that Generate Data
                services.AddHostedService<Feed1HostedService>();
                services.AddHostedService<Feed2HostedService>();

                services.AddEventHorizon(x =>
                {
                    // Add Stream
                    // x.AddInMemoryEventStream();
                    x.AddPulsarEventStream(context.Configuration.GetSection("Pulsar").Bind);

                    // Add Hosted Subscription
                    x.AddSubscription<PriceChangeTracker, Event>(h =>
                    {
                        h.AddStream<Feed1PriceChanged>();
                        h.AddStream<Feed2PriceChanged>();
                    });
                });

            })
            .UseSerilog((_, config) => { config
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .Destructure.UsingAttributes();
            })
            .UseEnvironment("local")
            .Build()
            .RunAsync();
    }
}
