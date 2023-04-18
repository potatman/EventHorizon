﻿using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.InMemory.Extensions;
using Insperex.EventHorizon.EventStreaming.Samples.Handlers;
using Insperex.EventHorizon.EventStreaming.Samples.HostedServices;
using Insperex.EventHorizon.EventStreaming.Samples.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Insperex.EventHorizon.EventStreaming.Samples;

class Program
{
    static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(new string[] { })
            .ConfigureServices((hostContext, services) =>
            {
                // Feeds that Generate Data
                services.AddHostedService<Feed1HostedService>();
                services.AddHostedService<Feed2HostedService>();
                
                // Add Stream
                services.AddInMemoryEventStream();
                // services.AddPulsarEventStream(hostContext.Configuration);
                
                // Add Hosted Subscription
                services.AddHostedSubscription<PriceChangeTracker, Event>(x =>
                {
                    x.AddActionTopic<Feed1PriceChanged>();
                    x.AddActionTopic<Feed2PriceChanged>();
                });
            }) 
            .UseSerilog((_, config) => { config.WriteTo.Console(); })
            .UseEnvironment("local")
            .Build()
            .RunAsync();
    }
}
