using System.Collections.Generic;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Interfaces;
using Insperex.EventHorizon.EventSourcing.Samples.Models.View;

namespace Insperex.EventHorizon.EventSourcing.Samples.Middleware
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
