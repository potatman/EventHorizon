using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventStore.Interfaces;

public interface IStateParent<T> : ICrudEntity
    where T : IState
{
    public long SequenceId { get; set; }
    public T State { get; set; }
}
