using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Admins;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Insperex.EventHorizon.EventStreaming.InMemory.Extensions
{
    public static class StreamingConfiguratorExtensions
    {
        public static EventHorizonConfigurator AddInMemoryEventStream(this EventHorizonConfigurator configurator)
        {
            configurator.Collection.Replace(ServiceDescriptor.Describe(
                typeof(IStreamFactory),
                typeof(InMemoryStreamFactory),
                ServiceLifetime.Singleton));

            configurator.Collection.Replace(ServiceDescriptor.Describe(
                typeof(ITopicAdmin<>),
                typeof(InMemoryTopicAdmin<>),
                ServiceLifetime.Singleton));

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
