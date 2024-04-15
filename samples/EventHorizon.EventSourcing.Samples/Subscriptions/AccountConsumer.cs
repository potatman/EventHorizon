using System.Linq;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventSourcing.Samples.Models.Actions;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Subscriptions;

namespace EventHorizon.EventSourcing.Samples.Subscriptions;

public class AccountConsumer : IStreamConsumer<Event>
{
    public Task OnBatch(SubscriptionContext<Event> context)
    {
        // TODO: Handle Subscription
        var events = context.Messages
            .Where(x => x.Data.Type == nameof(AccountCredited))
            .Select(x => x.GetPayload() as AccountCredited)
            .ToArray();
        return Task.CompletedTask;
    }
}
