using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventSourcing.Aggregates;

namespace Insperex.EventHorizon.EventSourcing.Interfaces
{
    public interface IAggregateMiddleware { }

    public interface IAggregateMiddleware<T> : IAggregateMiddleware
        where T : class, IState
    {
        public Task BeforeSave(Aggregate<T>[] aggregates);
        public Task AfterSave(Aggregate<T>[] aggregates);
    }
}
