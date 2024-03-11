using System;

namespace Insperex.EventHorizon.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class StoreAttribute : Attribute
{
    public string Database { get; set; }

    public StoreAttribute(string database)
    {
        Database = database;
    }
}
