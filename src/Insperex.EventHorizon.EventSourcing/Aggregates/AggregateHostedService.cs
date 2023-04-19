using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventSourcing.Util;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class AggregateHostedService<TParent, TAction, T> : IHostedService
    where TParent : class, IStateParent<T>, new()
    where T : class, IState
    where TAction : class, ITopicMessage, new()
{
    private readonly Aggregator<TParent, T> _aggregator;
    private readonly Subscription<TAction> _subscription;

    public AggregateHostedService(
        ValidationUtil validationUtil,
        StreamingClient streamingClient,
        AggregatorManager<TParent, T> aggregatorManager,
        Aggregator<TParent, T> aggregator)
    {
        _aggregator = aggregator;
        
        // Validate Handlers if Enabled
        if(aggregator.GetConfig().IsValidatingHandlers)
            validationUtil.Validate<TParent, T>();
        
        _subscription = streamingClient.CreateSubscription<TAction>()
            .SubscriptionName(typeof(T).Name)
            .AddStateTopic<T>()
            .OnBatch(x =>
            {
                var messages = x.Messages.Select(m => m.Data).ToArray();
                return aggregatorManager.Handle(messages, 0, x.CancellationToken);
            })
            .Build();
    }

    public async Task StartAsync(CancellationToken ct)
    {
        // Try to Refresh any Missing or Outdated Snapshots
        if (_aggregator.GetConfig().IsRebuildEnabled)
            await _aggregator.RebuildAllAsync(ct);

        // Start Command Subscription 
        await _subscription.StartAsync();
    }

    public Task StopAsync(CancellationToken ct)
    {
        return _subscription.StopAsync();
    }
}