using System.Collections.Generic;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStreaming;

namespace Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Workflows
{
    public class HandleAndApplyEventsWorkflow<TWrapper, TState, TMessage> : BaseSubscriptionWorkflow<TWrapper, TState, TMessage>
        where TWrapper : class, IStateParent<TState>, new()
        where TState : class, IState
        where TMessage : class, ITopicMessage, new()
    {
        private readonly WorkflowService<TWrapper, TState, TMessage> _workflowService;

        public HandleAndApplyEventsWorkflow(StreamingClient streamingClient,
            WorkflowService<TWrapper, TState, TMessage> workflowService,
            WorkflowConfigurator<TState> configurator) : base($"Handle{typeof(TMessage).Name}s", streamingClient, workflowService, configurator)
        {
            _workflowService = workflowService;
        }

        public override async Task HandleBatch(TMessage[] messages, Dictionary<string, Aggregate<TState>> aggregateDict)
        {
            // Map/Apply Changes
            _workflowService.TriggerHandle(messages, aggregateDict);

            // Save Snapshots and Publish Events
            await _workflowService.SaveAsync(aggregateDict);

            // Try to Publish Responses
            await _workflowService.TryAndPublishResponses(aggregateDict);
        }
    }
}
