using System;
using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Admins;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Client.Api;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Extensions
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
