using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventSourcing.Samples.Subscriptions;

public class AccountConsumer : IStreamConsumer<Event>
{
    public Task OnBatch(SubscriptionContext<Event> context)
    {
        // TODO: Handle Subscription
        return Task.CompletedTask;
    }
}
