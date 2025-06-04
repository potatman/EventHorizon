using System.Collections.Concurrent;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Subscriptions;

namespace EventHorizon.EventStreaming.Test.Shared;

public class ListStreamConsumer<T> : IStreamConsumer<T> where T : ITopicMessage
{
    public readonly BlockingCollection<MessageContext<T>> List = new();

    public Task OnBatch(SubscriptionContext<T> context)
    {
        foreach (var message in context.Messages)
            List.Add(message);

        return Task.CompletedTask;
    }
}
