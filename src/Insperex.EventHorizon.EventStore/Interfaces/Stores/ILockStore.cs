using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.Interfaces.Stores
{
    public interface ILockStore<T> : ICrudStore<Lock> where T : IState
    {

    }
}
