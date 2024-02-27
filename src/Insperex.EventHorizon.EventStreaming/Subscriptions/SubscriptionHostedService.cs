using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class SubscriptionHostedService<TMessage> : IHostedService
    where TMessage : ITopicMessage
{
    private readonly Subscription<TMessage>[] _subscriptions;

    public SubscriptionHostedService(IEnumerable<Subscription<TMessage>> subscriptions)
    {
        _subscriptions = subscriptions.ToArray();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _subscriptions)
            await subscription.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _subscriptions)
            await subscription.StopAsync();
    }
}
