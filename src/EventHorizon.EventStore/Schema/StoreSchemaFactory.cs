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
/// CLR shape and any <see cref="StoreFieldAttribute"/> intents. Schemas are cached per type.
/// </summary>
public class StoreSchemaFactory
{
    private const int MaxDepth = 8;
    private static readonly ConcurrentDictionary<Type, StoreFieldSchema> Cache = new();

    public StoreFieldSchema GetSchema(Type type) =>
        Cache.GetOrAdd(type, t => BuildNode(null, t, new HashSet<Type>(), 0));

    private static StoreFieldSchema BuildNode(PropertyInfo property, Type type, HashSet<Type> ancestors, int depth)
    {
        var attr = property?.GetCustomAttribute<StoreFieldAttribute>(true);
        var intent = attr?.Intent ?? FieldIntent.Default;
        var store = attr?.Store ?? true;
        var explicitIntent = intent != FieldIntent.Default || !store;

        var clrType = Nullable.GetUnderlyingType(type) ?? type;
        var isCollection = false;

        // Unwrap collections (byte[] stays a leaf: serialized as base64)
        if (clrType != typeof(string) && clrType != typeof(byte[]) && !IsDictionary(clrType))
        {
            var elementType = GetElementType(clrType);
            if (elementType != null)
            {
                isCollection = true;
                clrType = Nullable.GetUnderlyingType(elementType) ?? elementType;
            }
        }

        if (IsSimple(clrType))
            return new StoreFieldSchema
            {
                Property = property,
                ClrType = clrType,
                Intent = intent,
                Store = store,
                IsCollection = isCollection,
                HasExplicitIntents = explicitIntent
            };

        // Shapes that cannot be statically mapped stay schemaless
        if (IsDictionary(clrType) || ancestors.Contains(clrType) || depth >= MaxDepth)
            return new StoreFieldSchema
            {
                Property = property,
                ClrType = clrType,
                Intent = intent,
                Store = store,
                IsCollection = isCollection,
                IsOpaque = true,
                HasExplicitIntents = explicitIntent
            };

        ancestors.Add(clrType);
        var children = clrType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.CanRead && x.GetIndexParameters().Length == 0)
            .Select(x => BuildNode(x, x.PropertyType, ancestors, depth + 1))
            .ToArray();
        ancestors.Remove(clrType);

        return new StoreFieldSchema
        {
            Property = property,
            ClrType = clrType,
            Intent = intent,
            Store = store,
            IsCollection = isCollection,
            Children = children,
            HasExplicitIntents = explicitIntent || children.Any(x => x.HasExplicitIntents)
        };
    }

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

    private static bool IsDictionary(Type type) =>
        typeof(IDictionary).IsAssignableFrom(type)
        || GetGenericInterface(type, typeof(IDictionary<,>)) != null
        || GetGenericInterface(type, typeof(IReadOnlyDictionary<,>)) != null;

    private static Type GetElementType(Type type)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type)) return null;
        if (type.IsArray) return type.GetElementType();
        return GetGenericInterface(type, typeof(IEnumerable<>))?.GetGenericArguments()[0]
               ?? typeof(object);
    }

    private static Type GetGenericInterface(Type type, Type definition)
    {
        if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == definition)
            return type;
        return type.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == definition);
    }
}
