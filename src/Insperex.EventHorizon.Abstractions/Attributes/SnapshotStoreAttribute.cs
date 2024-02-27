using System;

namespace Insperex.EventHorizon.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class SnapshotStoreAttribute : Attribute
{
    public string Database { get; set; }

    public SnapshotStoreAttribute(string database)
    {
        Database = database;
    }
}
