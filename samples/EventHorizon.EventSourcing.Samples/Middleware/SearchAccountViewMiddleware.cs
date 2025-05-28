using System.Threading.Tasks;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventSourcing.Interfaces;
using EventHorizon.EventSourcing.Samples.Models.View;

namespace EventHorizon.EventSourcing.Samples.Middleware
{
    public class SearchAccountViewMiddleware : IAggregateMiddleware<SearchAccountView>
    {
        public Task OnLoad(Aggregate<SearchAccountView>[] aggregates)
        {
            return Task.CompletedTask;
        }

        public Task BeforeSave(Aggregate<SearchAccountView>[] aggregates)
        {
            return Task.CompletedTask;
        }

        public Task AfterSave(Aggregate<SearchAccountView>[] aggregates)
        {
            return Task.CompletedTask;
        }
    }
}
