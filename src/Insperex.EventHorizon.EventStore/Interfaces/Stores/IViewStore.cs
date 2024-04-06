using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.Interfaces.Stores
{
    public interface IViewStore<T> : ICrudStore<View<T>> where T : class, IState
    {

    }
}
