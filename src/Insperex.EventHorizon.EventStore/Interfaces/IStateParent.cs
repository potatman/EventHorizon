using System;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;

namespace Insperex.EventHorizon.EventStore.Interfaces;

public interface IStateParent<T> : ICrudEntity, ICompressible<T> where T : class
{
    public long SequenceId { get; set; }
}
