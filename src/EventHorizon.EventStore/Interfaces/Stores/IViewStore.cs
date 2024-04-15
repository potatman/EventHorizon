using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.Interfaces.Stores
{
    public interface IViewStore<T> : ICrudStore<View<T>> where T : class, IState
    {

    }
}
