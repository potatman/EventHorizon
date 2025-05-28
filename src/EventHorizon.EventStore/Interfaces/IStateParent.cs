using System;

namespace EventHorizon.EventStore.Interfaces;

public interface IStateParent<T> : ICrudEntity
{
    public long SequenceId { get; set; }
    public T State { get; set; }
}
