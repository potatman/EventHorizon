using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.Interfaces.Factory;

public interface IViewStoreFactory<T> where T : class, IState
{
    public ICrudStore<View<T>> GetViewStore();
}
