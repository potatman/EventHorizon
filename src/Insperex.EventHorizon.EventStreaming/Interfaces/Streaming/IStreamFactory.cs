using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface IStreamFactory
{
    ITopicProducer<TMessage> CreateProducer<TMessage>(PublisherConfig config) where TMessage : ITopicMessage;
    ITopicConsumer<TMessage> CreateConsumer<TMessage>(SubscriptionConfig<TMessage> config) where TMessage : ITopicMessage;
    ITopicReader<TMessage> CreateReader<TMessage>(ReaderConfig config) where TMessage : ITopicMessage;
    ITopicAdmin<TMessage> CreateAdmin<TMessage>() where TMessage : ITopicMessage;
}
