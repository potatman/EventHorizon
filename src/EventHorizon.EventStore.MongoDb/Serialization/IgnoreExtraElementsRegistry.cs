using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using MongoDB.Bson.Serialization.Conventions;

namespace EventHorizon.EventStore.MongoDb.Serialization;

/// <summary>
/// Makes the MongoDB driver ignore unknown document elements for the types EventHorizon stores, so removing or
/// renaming a state property does not break loading documents written before the change.
/// </summary>
/// <remarks>
/// The convention is scoped to the type graphs registered here rather than applied to every type in the process,
/// so applications that use the driver directly for their own collections keep the driver's default (throw).
/// Conventions only apply when the driver first builds a class map for a type, which for EventHorizon types happens
/// on the first read or write through a store, after the store factory has registered the graph.
/// Members typed as <see cref="object"/> or an interface are not walked; their runtime types keep the default.
/// </remarks>
internal static class IgnoreExtraElementsRegistry
{
    private const string ConventionName = "EventHorizon.IgnoreExtraElements";
    private static readonly ConcurrentDictionary<Type, bool> Types = new();

    static IgnoreExtraElementsRegistry()
    {
        var pack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
        ConventionRegistry.Register(ConventionName, pack, type => Types.ContainsKey(type));
    }

    /// <summary>
    /// Registers <paramref name="root"/> and every type reachable through its public properties, fields,
    /// generic arguments and element types.
    /// </summary>
    public static void Register(Type root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Visit(root);
    }

    /// <summary>
    /// True when the convention applies to <paramref name="type"/>.
    /// </summary>
    public static bool IsRegistered(Type type) => Types.ContainsKey(type);

    private static void Visit(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type.IsArray)
        {
            Visit(type.GetElementType());
            return;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
                Visit(argument);
        }

        if (!IsMappedType(type) || !Types.TryAdd(type, true))
            return;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length == 0)
                Visit(property.PropertyType);
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            Visit(field.FieldType);
    }

    // Only user-defined classes and structs get BSON class maps; primitives, enums, collections (serialized as
    // arrays or documents, their element types were already visited) and framework or driver types do not.
    private static bool IsMappedType(Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type.IsInterface || type.IsPointer || type.IsGenericParameter)
            return false;

        if (type == typeof(object) || typeof(IEnumerable).IsAssignableFrom(type))
            return false;

        var ns = type.Namespace ?? string.Empty;
        return !ns.StartsWith("System", StringComparison.Ordinal)
               && !ns.StartsWith("Microsoft", StringComparison.Ordinal)
               && !ns.StartsWith("MongoDB", StringComparison.Ordinal);
    }
}
