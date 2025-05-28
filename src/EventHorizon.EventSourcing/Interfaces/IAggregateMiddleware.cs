using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventSourcing.Aggregates;

namespace EventHorizon.EventSourcing.Interfaces
{
    public interface IAggregateMiddleware { }

    public interface IAggregateMiddleware<T> : IAggregateMiddleware
        where T : class, IState
    {
        public Task OnLoad(Aggregate<T>[] aggregates);
        public Task BeforeSave(Aggregate<T>[] aggregates);
        public Task AfterSave(Aggregate<T>[] aggregates);
    }
}
