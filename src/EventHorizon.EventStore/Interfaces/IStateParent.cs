using EventHorizon.Abstractions.Serialization.Compression;

namespace EventHorizon.EventStore.Interfaces;

public interface IStateParent<T> : ICrudEntity, ICompressible<T> where T : class
{
    public long SequenceId { get; set; }
}
