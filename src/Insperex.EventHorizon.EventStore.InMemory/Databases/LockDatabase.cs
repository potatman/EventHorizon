using System.Collections.Generic;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.InMemory.Databases;

public class LockDatabase
{
    public Dictionary<string, Lock> Locks = new();
}