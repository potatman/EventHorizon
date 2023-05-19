using System.Collections.Generic;
using Insperex.EventHorizon.EventStore.Interfaces;

namespace Insperex.EventHorizon.EventStore.InMemory.Databases;

public class CrudDatabase
{
    public readonly Dictionary<string, Dictionary<string, ICrudEntity>> CrudEntities = new();
}
