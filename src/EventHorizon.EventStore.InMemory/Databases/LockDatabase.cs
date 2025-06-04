using System.Collections.Generic;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.InMemory.Databases;

public class LockDatabase
{
    public Dictionary<string, Lock> Locks = new();
}
