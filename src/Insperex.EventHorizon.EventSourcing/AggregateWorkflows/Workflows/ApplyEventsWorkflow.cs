using System.Collections.Generic;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStreaming;

namespace Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Workflows
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

        public override async Task HandleBatch(Event[] events, Dictionary<string, Aggregate<TState>> aggregateDict)
        {
            // Map/Apply Changes
            _workflowService.TriggerApplyEvents(events, aggregateDict, false);

            // Save Snapshots and Publish Events
            await _workflowService.SaveAsync(aggregateDict);
        }
    }
}
