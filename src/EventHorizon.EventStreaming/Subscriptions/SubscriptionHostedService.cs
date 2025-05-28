using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using Microsoft.Extensions.Hosting;

namespace EventHorizon.EventStreaming.Subscriptions;

public class SubscriptionHostedService<TM> : IHostedService where TM : class, ITopicMessage, new()
{
    private readonly Subscription<TM>[] _subscriptions;

    public SubscriptionHostedService(IEnumerable<Subscription<TM>> subscriptions)
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
