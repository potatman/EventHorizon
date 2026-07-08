using System;
using System.Reflection;
using EventHorizon.Abstractions.Attributes;

namespace EventHorizon.EventStore.Schema;

/// <summary>
/// One node of the implementation-agnostic schema for a stored entity type. The root node
/// represents the entity itself (e.g. Snapshot&lt;T&gt;); children represent its properties.
/// Store implementations translate this model into their native mapping/indexing.
/// </summary>
public sealed record StoreFieldSchema
{
    /// <summary>Property this node maps; null for the root entity node.</summary>
    public PropertyInfo Property { get; init; }

    /// <summary>Value type: nullable unwrapped, element type when <see cref="IsCollection"/>.</summary>
    public Type ClrType { get; init; }

    public FieldIntent Intent { get; init; }

    /// <summary>False when the value should be queryable but not retrievable (see <see cref="StoreFieldAttribute.Store"/>).</summary>
    public bool Store { get; init; } = true;

    public bool IsCollection { get; init; }

    /// <summary>
    /// True when the shape is not statically knowable (dictionaries, type cycles, depth cap)
    /// and the field should be stored as a schemaless object.
    /// </summary>
    public bool IsOpaque { get; init; }

    /// <summary>Child fields for complex objects; null for leaf and opaque nodes.</summary>
    public StoreFieldSchema[] Children { get; init; }

    /// <summary>True when this node or any descendant declares an explicit <see cref="StoreFieldAttribute"/>.</summary>
    public bool HasExplicitIntents { get; init; }
}
