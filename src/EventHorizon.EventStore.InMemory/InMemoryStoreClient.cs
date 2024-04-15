using System.Collections.Generic;
using EventHorizon.EventStore.Interfaces;

namespace EventHorizon.EventStore.InMemory;

public class InMemoryStoreClient
{
    public readonly Dictionary<string, Dictionary<string, ICrudEntity>> Entities = new();
}
