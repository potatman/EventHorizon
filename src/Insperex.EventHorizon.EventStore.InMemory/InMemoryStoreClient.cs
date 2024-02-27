using System.Collections.Generic;
using Insperex.EventHorizon.EventStore.Interfaces;

namespace Insperex.EventHorizon.EventStore.InMemory;

public class InMemoryStoreClient
{
    public readonly Dictionary<string, Dictionary<string, ICrudEntity>> Entities = new();
}
