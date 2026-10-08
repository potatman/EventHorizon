using System.Collections.Concurrent;
using EventHorizon.EventStore.Interfaces;

namespace EventHorizon.EventStore.InMemory.Databases;

/// <summary>
/// Tables shared by every in-memory store resolved from one service provider, keyed by the stored entity type
/// (e.g. <c>Snapshot`1[My.Account]</c>), so snapshots, views and locks of the same state never share a table.
/// </summary>
public class CrudDatabase
{
    public readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ICrudEntity>> CrudEntities = new();
}
