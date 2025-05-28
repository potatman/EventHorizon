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
        private readonly Subscription<Event> _subscription;

        public AggregateMigrationHostedService(Aggregator<Snapshot<TTarget>, TTarget> aggregator,
            StreamingClient streamingClient,
            Func<SubscriptionBuilder<Event>, SubscriptionBuilder<Event>> onBuildSubscription = null)
        {
            var builder = streamingClient.CreateSubscription<Event>()
                .AddStream<TSource>()
                .SubscriptionName($"Migrate-{typeof(TSource).Name}-{typeof(TTarget).Name}")
                .OnBatch(async batch =>
                {
                    var events = batch.Messages
                        .Select(x => x.Data)
                        .ToArray();

                    var responses = await aggregator
                        .HandleAsync(events, batch.CancellationToken);

                    var failedIds = responses
                        .Where(x => x.Error != null)
                        .Select(x => x.StreamId)
                        .ToArray();

                    var failedMessages = batch.Messages
                        .Where(x => failedIds.Contains(x.Data.StreamId))
                        .ToArray();

                    batch.Nack(failedMessages);
                });

            if (onBuildSubscription != null) builder = onBuildSubscription(builder);

            _subscription = builder.Build();
        }

        public Task StartAsync(CancellationToken cancellationToken) => _subscription.StartAsync();
        public Task StopAsync(CancellationToken cancellationToken) => _subscription.StopAsync();
    }
}
