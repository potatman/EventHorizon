using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class SubscriptionHostedService<T> : IHostedService where T : class, ITopicMessage, new()
{
    private readonly Subscription<T> _subscription;

    public SubscriptionHostedService(Subscription<T> subscription)
    {
        _subscription = subscription;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _subscription.StartAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _subscription.StopAsync();
    }
}