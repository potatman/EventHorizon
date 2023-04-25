using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Extensions;
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
        StreamingClient streamingClient,
        Aggregator<TParent, T> aggregator)
    {
        _aggregator = aggregator;

        _subscription = streamingClient.CreateSubscription<TAction>()
            .SubscriptionName(typeof(T).Name)
            .AddStateTopic<T>()
            .OnBatch(async x =>
            {
                var messages = x.Messages.Select(m => m.Data).ToArray();
                var responses = await aggregator.Handle(messages, x.CancellationToken);
                await aggregator.PublishResponseAsync(responses);
            })
            .Build();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _aggregator.SetupAsync();

        // Try to Refresh any Missing or Outdated Snapshots
        if (_aggregator.GetConfig().IsRebuildEnabled)
            await _aggregator.RebuildAllAsync(cancellationToken);

        // Start Command Subscription
        await _subscription.StartAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _subscription.StopAsync();
    }
}
