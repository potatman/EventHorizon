using System.Threading.Tasks;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Interfaces;
using Insperex.EventHorizon.EventSourcing.Samples.Models.View;

namespace Insperex.EventHorizon.EventSourcing.Samples.Middleware
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
