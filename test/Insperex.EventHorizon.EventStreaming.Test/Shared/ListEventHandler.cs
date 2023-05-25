using System.Collections.Concurrent;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Shared;

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