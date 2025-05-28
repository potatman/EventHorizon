using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.Subscriptions;

namespace EventHorizon.EventStreaming.Interfaces.Streaming;

public interface IStreamConsumer<T> where T : ITopicMessage
{
    public Task OnBatch(SubscriptionContext<T> context);
}
