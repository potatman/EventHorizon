using System;
using System.Collections.Generic;

namespace Insperex.EventHorizon.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ViewStoreAttribute : Attribute
{
    public string Database { get; set; }
    public ViewStoreAttribute(string database)
    {
        Database = database;
    }
}
