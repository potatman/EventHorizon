using System.Collections.Generic;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStreaming;

namespace EventHorizon.EventSourcing.AggregateWorkflows.Workflows
{
    public class ApplyEventsWorkflow<TWrapper, TState> : BaseSubscriptionWorkflow<TWrapper, TState, Event>
        where TWrapper : class, IStateParent<TState>, new()
        where TState : class, IState
    {
        private readonly WorkflowService<TWrapper, TState, Event> _workflowService;

        public ApplyEventsWorkflow(StreamingClient streamingClient, WorkflowService<TWrapper, TState, Event> workflowService, WorkflowConfigurator<TWrapper, TState> configurator)
            : base("ApplyEvents", streamingClient, workflowService, configurator)
        {
            _workflowService = workflowService;
        }

        public override async Task HandleBatchAsync(Event[] events, Dictionary<string, Aggregate<TState>> aggregateDict)
        {
            // Map/Apply Changes
            _workflowService.TriggerApplyEvents(events, aggregateDict, false);

            // Save Snapshots and Publish Events
            await _workflowService.SaveAsync(aggregateDict).ConfigureAwait(false);
        }
    }
}
