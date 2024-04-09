using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Interfaces;
using Insperex.EventHorizon.EventSourcing.Extensions;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Workflows
{
    public abstract class BaseSubscriptionWorkflow<TWrapper, TState, TMessage> : IWorkflow<TState, TMessage>
        where TWrapper : class, IStateParent<TState>, new()
        where TState : class, IState
        where TMessage : class, ITopicMessage, new()
    {
        private readonly WorkflowService<TWrapper, TState, TMessage> _workflowService;
        private readonly Subscription<TMessage> _subscription;

        public BaseSubscriptionWorkflow(string name, StreamingClient streamingClient, WorkflowService<TWrapper, TState, TMessage> workflowService, WorkflowConfigurator<TWrapper, TState> configurator)
        {
            _workflowService = workflowService;
            var subscriptionBuilder = streamingClient.CreateSubscription<TMessage>()
                .SubscriptionName($"{name}-{typeof(TState).Name}")
                .AddStateStream<TState>()
                .BatchSize(configurator.BatchSize ?? 1000)
                .OnBatch(async batch =>
                {
                    // Setup
                    var messages = batch.Messages.Select(m => m.Data).ToArray();
                    var aggregateDict = await workflowService.LoadAsync(messages, batch.CancellationToken).ConfigureAwait(false);

                    await HandleBatchAsync(messages, aggregateDict).ConfigureAwait(false);

                    batch.NackFailedMessagesOnAggregates(aggregateDict);
                });

            _subscription = subscriptionBuilder.Build();
        }

        public Task Handle(TMessage message, CancellationToken ct) => HandleBatchAsync([message], ct);

        public async Task HandleBatchAsync(TMessage[] messages, CancellationToken ct)
        {
            var aggregateDict = await _workflowService.LoadAsync(messages, ct).ConfigureAwait(false);
            await HandleBatchAsync(messages, aggregateDict).ConfigureAwait(false);
        }

        public abstract Task HandleBatchAsync(TMessage[] messages, Dictionary<string, Aggregate<TState>> aggregateDict);

        public Task StartAsync(CancellationToken cancellationToken) => _subscription.StartAsync();
        public Task StopAsync(CancellationToken cancellationToken) => _subscription.StopAsync();
    }
}
