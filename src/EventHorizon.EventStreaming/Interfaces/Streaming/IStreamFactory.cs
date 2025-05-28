using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.Publishers;
using EventHorizon.EventStreaming.Readers;
using EventHorizon.EventStreaming.Subscriptions;

namespace EventHorizon.EventStreaming.Interfaces.Streaming;

public interface IStreamFactory
{
    ITopicProducer<T> CreateProducer<T>(PublisherConfig config) where T : class, ITopicMessage, new();
    ITopicConsumer<T> CreateConsumer<T>(SubscriptionConfig<T> config) where T : class, ITopicMessage, new();
    ITopicReader<T> CreateReader<T>(ReaderConfig config) where T : class, ITopicMessage, new();
    ITopicAdmin<T> CreateAdmin<T>() where T : ITopicMessage;
    ITopicResolver GetTopicResolver();
}
