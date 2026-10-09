using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStreaming;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Hosting;

namespace EventHorizon.EventSourcing.Aggregates
{
    public class AggregateMigrationHostedService<TSource, TTarget> : IHostedService
        where TSource : class, IState, new()
        where TTarget : class, IState, new()
    {
        private readonly Aggregator<Snapshot<TTarget>, TTarget> _aggregator;
        private readonly Subscription<Event> _subscription;
        private readonly CancellationTokenSource _stopping = new();

        public AggregateMigrationHostedService(Aggregator<Snapshot<TTarget>, TTarget> aggregator,
            StreamingClient streamingClient,
            Func<SubscriptionBuilder<Event>, SubscriptionBuilder<Event>> onBuildSubscription = null)
        {
            _aggregator = aggregator;
            var builder = streamingClient.CreateSubscription<Event>()
                .AddStream<TSource>()
                .SubscriptionName($"Migrate-{typeof(TSource).Name}-{typeof(TTarget).Name}")
                .OnBatch(async batch =>
                {
                    await aggregator.HandleWithRetryAsync(batch.Messages, batch.Nack, _stopping.Token);
                });

            if (onBuildSubscription != null) builder = onBuildSubscription(builder);

            _subscription = builder.Build();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _aggregator.EnsureStoreSetupAsync(cancellationToken);
            await _subscription.StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _stopping.CancelAsync();
            await _subscription.StopAsync();
        }
    }
}
