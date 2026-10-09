using System;

namespace EventHorizon.EventStore.Schema;

/// <summary>
/// Thrown when a stored entity's type graph is too large to build a <see cref="StoreFieldSchema"/>.
/// Stores either fall back to schemaless behavior or surface it when a static schema was demanded.
/// </summary>
internal sealed class StoreSchemaLimitException : InvalidOperationException
{
    public StoreSchemaLimitException(string message) : base(message)
    {
    }
}
