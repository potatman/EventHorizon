using System;
using EventHorizon.Abstractions;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStreaming.Admins;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Publishers;
using EventHorizon.EventStreaming.Pulsar.Models;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulsar.Client.Api;

namespace EventHorizon.EventStreaming.Pulsar.Extensions
{
    public static class StreamingConfiguratorExtensions
    {
        public static EventHorizonConfigurator AddPulsarEventStream(this EventHorizonConfigurator configurator, Action<PulsarConfig> onConfig)
        {
            // Add Admin and Factory
            configurator.Collection.Configure(onConfig);
            configurator.Collection.AddSingleton<PulsarClientResolver>();
            configurator.Collection.AddSingleton(typeof(ITopicAdmin<>), typeof(PulsarTopicAdmin<>));
            configurator.Collection.AddSingleton(typeof(IStreamFactory), typeof(PulsarStreamFactory));

            // Common
            configurator.Collection.AddSingleton(typeof(StreamingClient));
            configurator.Collection.AddSingleton(typeof(PublisherBuilder<>));
            configurator.Collection.AddSingleton(typeof(ReaderBuilder<>));
            configurator.Collection.AddSingleton(typeof(SubscriptionBuilder<>));
            configurator.Collection.AddSingleton(typeof(Admin<>));
            configurator.Collection.AddSingleton<AttributeUtil>();

            return configurator;
        }
    }
}
