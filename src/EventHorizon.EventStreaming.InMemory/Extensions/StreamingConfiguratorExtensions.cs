using EventHorizon.Abstractions;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStreaming.Admins;
using EventHorizon.EventStreaming.InMemory.Databases;
using EventHorizon.EventStreaming.InMemory.Failure;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Publishers;
using EventHorizon.EventStreaming.Readers;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventHorizon.EventStreaming.InMemory.Extensions
{
    public static class StreamingConfiguratorExtensions
    {
        public static EventHorizonConfigurator AddInMemoryEventStream(this EventHorizonConfigurator configurator)
        {
            configurator.Collection.AddSingleton<MessageDatabase>();
            configurator.Collection.AddSingleton<ConsumerDatabase>();
            configurator.Collection.AddSingleton<IndexDatabase>();

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
            configurator.Collection.AddSingleton<FailureHandlerFactory>();

            return configurator;
        }
    }
}
