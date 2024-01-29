using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class SubscriptionHostedService<TH, TM> : IHostedService where TM : class, ITopicMessage, new()
{
    private readonly Subscription<TM> _subscription;

    public SubscriptionHostedService(Subscription<TM> subscription)
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