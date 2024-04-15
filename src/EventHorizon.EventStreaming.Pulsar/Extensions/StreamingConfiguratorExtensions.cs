using System;
using EventHorizon.Abstractions;
using EventHorizon.Abstractions.Formatters;
using EventHorizon.EventStreaming.Admins;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Publishers;
using EventHorizon.EventStreaming.Pulsar.Models;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pulsar.Client.Api;

namespace EventHorizon.EventStreaming.Pulsar.Extensions
{
    public static class StreamingConfiguratorExtensions
    {
        public static EventHorizonConfigurator AddPulsarClient(this EventHorizonConfigurator configurator, Action<PulsarConfig> onConfig)
        {
            configurator.Collection.Configure(onConfig);
            configurator.AddClientResolver<PulsarClientResolver, PulsarClient>();
            return configurator;
        }

        public static EventHorizonConfigurator AddPulsarEventStream(this EventHorizonConfigurator configurator, Action<PulsarConfig> onConfig)
        {
            // Add Admin and Factory
            AddPulsarClient(configurator, onConfig);

            // Pulsar
            configurator.Collection.AddSingleton(typeof(IStreamFactory), typeof(PulsarStreamFactory));
            configurator.Collection.Replace(new ServiceDescriptor(typeof(ITopicFormatter), typeof(PulsarTopicFormatter), ServiceLifetime.Singleton));

            // Common
            configurator.Collection.AddSingleton<StreamingClient>();
            configurator.Collection.AddSingleton(typeof(PublisherBuilder<>));
            configurator.Collection.AddSingleton(typeof(ReaderBuilder<>));
            configurator.Collection.AddSingleton(typeof(SubscriptionBuilder<>));
            configurator.Collection.AddSingleton(typeof(Admin<>));

            return configurator;
        }
    }
}
