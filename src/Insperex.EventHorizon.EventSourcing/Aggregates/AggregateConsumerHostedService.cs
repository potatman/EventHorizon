using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class AggregateConsumerHostedService<TParent, TMessage, T> : IHostedService
    where TParent : IStateParent<T>, new()
    where T : IState
    where TMessage : class, ITopicMessage
{
    private readonly Aggregator<TParent, T> _aggregator;
    private readonly Subscription<TMessage> _subscription;

    public AggregateConsumerHostedService(
        StreamingClient<TMessage> streamingClient,
        Aggregator<TParent, T> aggregator,
        Func<SubscriptionBuilder<TMessage>, SubscriptionBuilder<TMessage>> onBuildSubscription = null)
    {
        _aggregator = aggregator;

        var config = _aggregator.GetConfig();

        var builder = streamingClient.CreateSubscription()
            .SubscriptionName($"Apply-{typeof(TMessage).Name}-{typeof(T).Name}")
            .AddStateStream<T>()
            .OnBatch(async x =>
            {
                var messages = x.Messages.Select(m => m.Data).ToArray();
                var responses = await aggregator.HandleAsync(messages, x.CancellationToken);
                await aggregator.PublishResponseAsync(responses);
            });

        if (onBuildSubscription != null) builder = onBuildSubscription(builder);

        if(config.BatchSize != null)
            builder = builder.BatchSize(config.BatchSize.Value);

        _subscription = builder.Build();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
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
