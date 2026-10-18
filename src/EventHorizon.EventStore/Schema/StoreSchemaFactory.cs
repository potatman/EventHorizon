using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EventHorizon.Abstractions.Attributes;

namespace EventHorizon.EventStore.Schema;

/// <summary>
/// Builds the <see cref="StoreFieldSchema"/> for a stored entity type by reflecting over its
/// CLR shape and any <see cref="StoreFieldAttribute"/> intents. Schemas (and build failures)
/// are cached per type, so callers should only request one when they will use it.
/// </summary>
internal static class StoreSchemaFactory
{
    /// <summary>Nesting depth beyond which objects are treated as opaque.</summary>
    internal const int MaxDepth = 8;

    /// <summary>Upper bound on schema nodes; shared nested types fan out multiplicatively.</summary>
    internal const int MaxNodes = 10_000;

    // Framework and driver types (Type, BsonDocument, JsonElement, ...) serialize with custom
    // converters whose output does not follow their reflected properties.
    private static readonly string[] OpaqueNamespaces = { "System", "Microsoft", "MongoDB", "Elastic" };

    private static readonly ConcurrentDictionary<Type, Lazy<StoreFieldSchema>> Cache = new();

    /// <exception cref="StoreSchemaLimitException">The type graph exceeds <see cref="MaxNodes"/>.</exception>
    /// <exception cref="InvalidOperationException">A property declares an invalid intent combination.</exception>
    public static StoreFieldSchema GetSchema(Type type) =>
        Cache.GetOrAdd(type, t => new Lazy<StoreFieldSchema>(() => Build(t))).Value;

    private static StoreFieldSchema Build(Type type)
    {
        var context = new BuildContext(type);
        return BuildNode(null, type, context, 0);
    }

    private static StoreFieldSchema BuildNode(PropertyInfo property, Type type, BuildContext context, int depth)
    {
        if (++context.NodeCount > MaxNodes)
            throw new StoreSchemaLimitException(
                $"The type graph of {context.RootType} exceeds {MaxNodes} fields; its store schema cannot be built.");

        var attr = property?.GetCustomAttribute<StoreFieldAttribute>(true);
        var intent = attr?.Intent ?? FieldIntent.Default;
        ValidateIntent(property, intent);

        var node = new StoreFieldSchema
        {
            Property = property,
            Intent = intent,
            IsAnnotated = attr is not null,
            HasExplicitIntents = attr is not null
        };

        var clrType = Unwrap(type);
        if (IsSimple(clrType))
            return node with { ClrType = clrType };

        var isCollection = false;
        if (TryGetElementType(clrType, out var elementType))
        {
            isCollection = true;
            clrType = Unwrap(elementType);
            if (IsSimple(clrType))
                return node with { ClrType = clrType, IsCollection = true };

            // Nested collections have no stable object shape
            if (TryGetElementType(clrType, out _))
                return node with { ClrType = clrType, IsCollection = true, IsOpaque = true };
        }

        if (IsOpaqueType(clrType) || context.Ancestors.Contains(clrType) || depth >= MaxDepth)
            return node with { ClrType = clrType, IsCollection = isCollection, IsOpaque = true };

        context.Ancestors.Add(clrType);
        var children = GetSerializedProperties(clrType)
            .Select(x => BuildNode(x, x.PropertyType, context, depth + 1))
            .ToArray();
        context.Ancestors.Remove(clrType);

        return node with
        {
            ClrType = clrType,
            IsCollection = isCollection,
            Children = children,
            HasExplicitIntents = node.HasExplicitIntents || children.Any(x => x.HasExplicitIntents)
        };
    }

    private static void ValidateIntent(PropertyInfo property, FieldIntent intent)
    {
        if (intent.HasFlag(FieldIntent.NotQueried) && intent != FieldIntent.NotQueried)
            throw new InvalidOperationException(
                $"{property?.DeclaringType?.Name}.{property?.Name}: {nameof(FieldIntent)}.{nameof(FieldIntent.NotQueried)} cannot be combined with other intents.");
    }

    /// <summary>
    /// Public readable instance properties, one per name. A property hidden with <c>new</c>
    /// resolves to the most-derived declaration, as System.Text.Json does.
    /// </summary>
    private static IEnumerable<PropertyInfo> GetSerializedProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetMethod is { IsPublic: true } && x.GetIndexParameters().Length == 0)
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(x => GetInheritanceDepth(x.DeclaringType)).First());

    private static int GetInheritanceDepth(Type type)
    {
        var depth = 0;
        for (var current = type?.BaseType; current is not null; current = current.BaseType)
            depth++;
        return depth;
    }

    private static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private static bool IsSimple(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(DateOnly)
        || type == typeof(TimeOnly)
        || type == typeof(TimeSpan)
        || type == typeof(Guid)
        || type == typeof(Uri)
        || type == typeof(byte[]);

    private static bool IsOpaqueType(Type type) =>
        type == typeof(object)
        || type.IsInterface
        || IsDictionary(type)
        || IsFrameworkType(type);

    private static bool IsFrameworkType(Type type)
    {
        var ns = type.Namespace;
        if (ns is null) return false;
        foreach (var prefix in OpaqueNamespaces)
        {
            if (ns.Length == prefix.Length ? ns == prefix : ns.StartsWith(prefix + ".", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsDictionary(Type type) =>
        typeof(IDictionary).IsAssignableFrom(type)
        || GetGenericInterface(type, typeof(IDictionary<,>)) is not null
        || GetGenericInterface(type, typeof(IReadOnlyDictionary<,>)) is not null;

    /// <summary>
    /// Element type of arrays and of enumerable types that serialize as JSON/BSON arrays
    /// (System.Collections types and user collections). Other framework enumerables such as
    /// BsonDocument are not unwrapped; they stay opaque.
    /// </summary>
    private static bool TryGetElementType(Type type, out Type elementType)
    {
        elementType = null;
        if (type == typeof(string) || type == typeof(byte[]) || IsDictionary(type)
            || !typeof(IEnumerable).IsAssignableFrom(type))
            return false;

        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return true;
        }

        var ns = type.Namespace ?? string.Empty;
        if (IsFrameworkType(type) && ns != "System.Collections" && !ns.StartsWith("System.Collections.", StringComparison.Ordinal))
            return false;

        elementType = GetGenericInterface(type, typeof(IEnumerable<>))?.GetGenericArguments()[0] ?? typeof(object);
        return true;
    }

    private static Type GetGenericInterface(Type type, Type definition)
    {
        if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == definition)
            return type;
        return type.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == definition);
    }

    private sealed class BuildContext
    {
        public BuildContext(Type rootType)
        {
            RootType = rootType;
        }

        public Type RootType { get; }
        public HashSet<Type> Ancestors { get; } = new();
        public int NodeCount { get; set; }
    }
}
