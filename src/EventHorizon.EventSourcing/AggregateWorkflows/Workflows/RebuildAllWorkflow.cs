using System.Collections.Generic;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStreaming;

namespace EventHorizon.EventSourcing.AggregateWorkflows.Workflows
{
    public class RebuildAllWorkflow<TWrapper, TState> : BaseSubscriptionWorkflow<TWrapper, TState, Event>
        where TWrapper : class, IStateParent<TState>, new()
        where TState : class, IState
    {
        private readonly Aggregator<TWrapper, TState> _aggregator;

        public RebuildAllWorkflow(Aggregator<TWrapper, TState> aggregator,
            StreamingClient streamingClient, WorkflowService<TWrapper, TState, Event> workflowService,
            WorkflowConfigurator<TWrapper, TState> configurator) : base("RebuildEvents", streamingClient, workflowService, configurator)
        {
            _aggregator = aggregator;
        }

        public override async Task HandleBatchAsync(Event[] messages, Dictionary<string, Aggregate<TState>> aggregateDict)
        {
            // Apply Events
            foreach (var message in messages)
                aggregateDict[message.StreamId].Apply(message, false);

            // Save Changes
            await _aggregator.SaveAllAsync(aggregateDict).ConfigureAwait(false);
        }
    }
}
