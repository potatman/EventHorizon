using System.Linq;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Actions;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventSourcing.Samples.Subscriptions;

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
