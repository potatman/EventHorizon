using System;

namespace EventHorizon.Abstractions.Attributes;

/// <summary>
/// Declares how a state property is used by queries so the configured event store can map or
/// index it efficiently. The declaration is store-agnostic: swapping store implementations
/// keeps the state class compiling and each store honors the intent as best it can.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class StoreFieldAttribute : Attribute
{
    public FieldIntent Intent { get; set; } = FieldIntent.Default;

    /// <summary>
    /// When false the value is indexed per its intent but not persisted retrievably
    /// (e.g. excluded from the ElasticSearch _source). Reads return the field as null.
    /// </summary>
    public bool Store { get; set; } = true;

    public StoreFieldAttribute()
    {
    }

    public StoreFieldAttribute(FieldIntent intent)
    {
        Intent = intent;
    }
}
