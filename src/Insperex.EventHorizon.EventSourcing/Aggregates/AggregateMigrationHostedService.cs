using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.EventSourcing.Aggregates
{
    public class AggregateMigrationHostedService<T> : IHostedService
        where T : class, IState, new()
    {
        private readonly Subscription<Event> _subscription;

        public AggregateMigrationHostedService(Aggregator<Snapshot<T>, T> aggregator, StreamingClient streamingClient)
        {
            _subscription = streamingClient.CreateSubscription<Event>()
                .AddStateTopic<T>()
                .SubscriptionName($"Migrate-{typeof(T).Name}")
                .OnBatch(async batch =>
                {
                    var events = batch.Messages
                        .Select(x => x.Data)
                        .ToArray();

                    var responses = await aggregator
                        .HandleAsync(events, batch.CancellationToken);

                    var failedIds = responses
                        .Where(x => x.Status != AggregateStatus.Ok)
                        .Select(x => x.StreamId)
                        .ToArray();

                    var failedMessages = batch.Messages
                        .Where(x => failedIds.Contains(x.Data.StreamId))
                        .ToArray();

                    batch.Nack(failedMessages);
                })
                .Build();
        }

        public Task StartAsync(CancellationToken cancellationToken) => _subscription.StartAsync();
        public Task StopAsync(CancellationToken cancellationToken) => _subscription.StopAsync();
    }
}
