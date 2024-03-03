using System.Collections.Concurrent;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventStreaming.Test.Shared;

public class ListStreamConsumer<TMessage> : IStreamConsumer<TMessage>
    where TMessage : ITopicMessage
{
    public readonly BlockingCollection<MessageContext<TMessage>> List = new();

    public Task OnBatch(SubscriptionContext<TMessage> context)
    {
        foreach (var message in context.Messages)
            List.Add(message);

        return Task.CompletedTask;
    }
}
