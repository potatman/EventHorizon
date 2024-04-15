using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.Interfaces.Stores
{
    public interface ILockStore<T> : ICrudStore<Lock> where T : IState
    {

    }
}
