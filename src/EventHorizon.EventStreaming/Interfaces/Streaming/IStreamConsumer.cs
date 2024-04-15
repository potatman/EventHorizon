using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.Subscriptions;

namespace EventHorizon.EventStreaming.Interfaces.Streaming;

public interface IStreamConsumer<TMessage> where TMessage : ITopicMessage
{
    public Task OnBatch(SubscriptionContext<TMessage> context);
}
