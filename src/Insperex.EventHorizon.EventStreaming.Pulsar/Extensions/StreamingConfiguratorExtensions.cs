using System;
using System.Net.Http;
using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Admins;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Client.Api;
using SharpPulsar.Admin.v2;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Extensions
{
    public static class StreamingConfiguratorExtensions
    {
        public static EventHorizonConfigurator AddPulsarEventStream(this EventHorizonConfigurator configurator)
        {
            var section = configurator.Config.GetSection("Pulsar");
            var config = section.Get<PulsarConfig>();
            if (config == null)
                return configurator;

            // Add Admin and Factory
            configurator.Collection.Configure<PulsarConfig>(section);
            configurator.Collection.AddSingleton<PulsarClientResolver>();
            configurator.Collection.AddSingleton<ITopicAdmin, PulsarTopicAdmin>();
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
