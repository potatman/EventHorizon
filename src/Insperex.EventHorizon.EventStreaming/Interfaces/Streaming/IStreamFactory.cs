using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface IStreamFactory<TMessage>
    where TMessage : ITopicMessage
{
    ITopicProducer<TMessage> CreateProducer(PublisherConfig config);
    ITopicConsumer<TMessage> CreateConsumer(SubscriptionConfig<TMessage> config);
    ITopicReader<TMessage> CreateReader(ReaderConfig config);
    ITopicAdmin<TMessage> CreateAdmin();
}
