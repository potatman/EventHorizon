using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.Publishers;
using EventHorizon.EventStreaming.Readers;
using EventHorizon.EventStreaming.Subscriptions;

namespace EventHorizon.EventStreaming.Interfaces.Streaming;

public interface IStreamFactory
{
    ITopicProducer<TMessage> CreateProducer<TMessage>(PublisherConfig config) where TMessage : ITopicMessage;
    ITopicConsumer<TMessage> CreateConsumer<TMessage>(SubscriptionConfig<TMessage> config) where TMessage : ITopicMessage;
    ITopicReader<TMessage> CreateReader<TMessage>(ReaderConfig config) where TMessage : ITopicMessage;
    ITopicAdmin<TMessage> CreateAdmin<TMessage>() where TMessage : ITopicMessage;
}
