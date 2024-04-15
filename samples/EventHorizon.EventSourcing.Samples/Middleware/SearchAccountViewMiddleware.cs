using System.Collections.Generic;
using System.Threading.Tasks;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventSourcing.AggregateWorkflows.Interfaces;
using EventHorizon.EventSourcing.Samples.Models.View;

namespace EventHorizon.EventSourcing.Samples.Middleware
{
    public class SearchAccountViewMiddleware : IWorkflowMiddleware<SearchAccountView>
    {
        public Task OnLoad(Dictionary<string, Aggregate<SearchAccountView>> aggregateDict)
        {
            return Task.CompletedTask;
        }

        public Task BeforeSave(Dictionary<string, Aggregate<SearchAccountView>> aggregateDict)
        {
            return Task.CompletedTask;
        }

        public Task AfterSave(Dictionary<string, Aggregate<SearchAccountView>> aggregateDict)
        {
            return Task.CompletedTask;
        }
    }
}
