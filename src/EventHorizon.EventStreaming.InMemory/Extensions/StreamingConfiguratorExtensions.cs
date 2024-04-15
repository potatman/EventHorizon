using EventHorizon.Abstractions;
using EventHorizon.Abstractions.Formatters;
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
        public static EventHorizonConfigurator AddInMemoryStreamClient(this EventHorizonConfigurator configurator)
        {
            configurator.Collection.AddSingleton<MessageDatabase>();
            configurator.Collection.AddSingleton<ConsumerDatabase>();
            configurator.Collection.AddSingleton<IndexDatabase>();
            return configurator;
        }

        public static EventHorizonConfigurator AddInMemoryEventStream(this EventHorizonConfigurator configurator)
        {
            AddInMemoryStreamClient(configurator);

            // Pulsar
            configurator.Collection.AddSingleton(typeof(IStreamFactory), typeof(InMemoryStreamFactory));
            configurator.Collection.Replace(new ServiceDescriptor(typeof(ITopicFormatter), typeof(InMemoryTopicFormatter), ServiceLifetime.Singleton));

            configurator.Collection.AddSingleton<StreamingClient>();
            configurator.Collection.AddSingleton(typeof(PublisherBuilder<>));
            configurator.Collection.AddSingleton(typeof(ReaderBuilder<>));
            configurator.Collection.AddSingleton(typeof(SubscriptionBuilder<>));
            configurator.Collection.AddSingleton(typeof(Admin<>));
            configurator.Collection.AddSingleton<FailureHandlerFactory>();

            return configurator;
        }
    }
}
