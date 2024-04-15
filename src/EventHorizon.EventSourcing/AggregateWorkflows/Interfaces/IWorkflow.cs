using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventSourcing.Aggregates;

namespace EventHorizon.EventSourcing.AggregateWorkflows.Interfaces
{
    public interface IWorkflow
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }

    public interface IWorkflow<TState, in TMessage> : IWorkflow
        where TMessage : ITopicMessage
        where TState : class, IState
    {
        Task HandleBatchAsync(TMessage[] events, Dictionary<string, Aggregate<TState>> aggregateDict);
    }
}
