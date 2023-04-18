using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface IStreamFactory
{
    ITopicProducer<T> CreateProducer<T>(PublisherConfig config) where T : class, ITopicMessage, new();
    ITopicConsumer<T> CreateConsumer<T>(SubscriptionConfig<T> config) where T : class, ITopicMessage, new();
    ITopicReader<T> CreateReader<T>(ReaderConfig config) where T : class, ITopicMessage, new();
    ITopicAdmin CreateAdmin();
    ITopicResolver GetTopicResolver();
}