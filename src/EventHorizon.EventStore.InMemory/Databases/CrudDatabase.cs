using System.Collections.Generic;
using EventHorizon.EventStore.Interfaces;

namespace EventHorizon.EventStore.InMemory.Databases;

public class CrudDatabase
{
    public readonly Dictionary<string, Dictionary<string, ICrudEntity>> CrudEntities = new();
}
