using System.Collections.Generic;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventSourcing.Aggregates;

namespace Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Interfaces
{
    public interface IWorkflowMiddleware { }

    public interface IWorkflowMiddleware<T> : IWorkflowMiddleware
        where T : IState
    {
        Task OnLoad(Dictionary<string, Aggregate<T>> aggregateDict);
        Task BeforeSave(Dictionary<string, Aggregate<T>> aggregateDict);
        Task AfterSave(Dictionary<string, Aggregate<T>> aggregateDict);
    }
}
