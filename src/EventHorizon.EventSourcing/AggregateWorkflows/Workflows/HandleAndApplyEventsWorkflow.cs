using System.Collections.Generic;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStreaming;

namespace EventHorizon.EventSourcing.AggregateWorkflows.Workflows
{
    public class HandleAndApplyEventsWorkflow<TWrapper, TState, TMessage> : BaseSubscriptionWorkflow<TWrapper, TState, TMessage>
        where TWrapper : class, IStateParent<TState>, new()
        where TState : class, IState
        where TMessage : class, ITopicMessage, new()
    {
        private readonly WorkflowService<TWrapper, TState, TMessage> _workflowService;

        public HandleAndApplyEventsWorkflow(StreamingClient streamingClient,
            WorkflowService<TWrapper, TState, TMessage> workflowService,
            WorkflowConfigurator<TWrapper, TState> configurator) : base($"Handle{typeof(TMessage).Name}s", streamingClient, workflowService, configurator)
        {
            _workflowService = workflowService;
        }

        public override async Task HandleBatchAsync(TMessage[] messages, Dictionary<string, Aggregate<TState>> aggregateDict)
        {
            // Map/Apply Changes
            _workflowService.TriggerHandle(messages, aggregateDict);

            // Save Snapshots and Publish Events
            await _workflowService.SaveAsync(aggregateDict).ConfigureAwait(false);

            // Try to Publish Responses
            await _workflowService.TryAndPublishResponses(aggregateDict).ConfigureAwait(false);
        }
    }
}
