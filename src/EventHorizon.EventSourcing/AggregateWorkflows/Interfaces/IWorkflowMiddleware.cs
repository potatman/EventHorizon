using System.Collections.Generic;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventSourcing.Aggregates;

namespace EventHorizon.EventSourcing.AggregateWorkflows.Interfaces
{
    public interface IWorkflowMiddleware { }

    public interface IWorkflowMiddleware<T> : IWorkflowMiddleware
        where T : class, IState
    {
        Task OnLoad(Dictionary<string, Aggregate<T>> aggregateDict);
        Task BeforeSave(Dictionary<string, Aggregate<T>> aggregateDict);
        Task AfterSave(Dictionary<string, Aggregate<T>> aggregateDict);
    }
}
