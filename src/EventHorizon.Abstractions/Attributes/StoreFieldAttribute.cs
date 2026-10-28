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
    /// <summary>
    /// How the field is queried. Intents can be combined as flags, except
    /// <see cref="FieldIntent.NotQueried"/>, which must stand alone.
    /// </summary>
    public FieldIntent Intent { get; set; } = FieldIntent.Default;

    public StoreFieldAttribute()
    {
    }

    public StoreFieldAttribute(FieldIntent intent)
    {
        Intent = intent;
    }
}
