using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.EventStreaming.Admins;
using Insperex.EventHorizon.EventStreaming.InMemory.Databases;
using Insperex.EventHorizon.EventStreaming.InMemory.Failure;
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
            configurator.Collection.Replace(ServiceDescriptor.Describe(
                typeof(IStreamFactory),
                typeof(InMemoryStreamFactory),
                ServiceLifetime.Singleton));
            configurator.Collection.Replace(ServiceDescriptor.Describe(
                typeof(ITopicFormatter),
                typeof(InMemoryTopicFormatter),
                ServiceLifetime.Singleton));

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
