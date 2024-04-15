using System.Collections.Concurrent;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Subscriptions;

namespace EventHorizon.EventStreaming.Test.Shared;

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
