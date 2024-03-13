using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventSourcing.Aggregates;

namespace Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Interfaces
{
    public interface IWorkflow
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }

    public interface IWorkflow<TState, in TMessage> : IWorkflow
        where TMessage : ITopicMessage
        where TState : IState
    {
        Task HandleBatch(TMessage[] events, Dictionary<string, Aggregate<TState>> aggregateDict);
    }
}
