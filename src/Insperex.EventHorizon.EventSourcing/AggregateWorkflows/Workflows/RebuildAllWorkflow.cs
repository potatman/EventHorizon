using System.Collections.Generic;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStreaming;

namespace Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Workflows
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

        public override async Task HandleBatch(Event[] messages, Dictionary<string, Aggregate<TState>> aggregateDict)
        {
            // Apply Events
            foreach (var message in messages)
                aggregateDict[message.StreamId].Apply(message, false);

            // Save Changes
            await _aggregator.SaveAllAsync(aggregateDict);
        }
    }
}
