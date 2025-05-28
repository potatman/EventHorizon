using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Interfaces.Internal;

namespace EventHorizon.Abstractions.Util;

/// <summary>
/// Note: this class allows tests to change bucketId
/// </summary>
public class AttributeUtil
{
    private readonly ConcurrentDictionary<string, object[]> _stateCache = new();
    private readonly ConcurrentDictionary<Type, PropertyInfo> _propertyInfoCache = new();

    public T GetOne<T>(Type type) where T : Attribute => GetAll<T>(type).FirstOrDefault();

    public T[] GetAll<T>(Type type) where T : Attribute
    {
        var key = GetKey<T>(type);

        if (_stateCache.TryGetValue(key, out var value))
            return value as T[];

        // Get Attributes on type and interfaces
        var attribute = type.GetCustomAttributes<T>()
            .Concat(type.GetInterfaces().SelectMany(x => x.GetCustomAttributes<T>()))
            .ToArray();

        // Set Cache with current value
        Set(type, attribute);

        return attribute;
    }

    public PropertyInfo GetOnePropertyInfo<T>(Type type) where T : Attribute
    {
        // Defensive
        if (_propertyInfoCache.TryGetValue(type, out var info)) return info;

        // Check Action
        var actionPropertyInfo = type.GetProperties()
            .FirstOrDefault(x => x.GetCustomAttribute<T>(true) != null);
        if (actionPropertyInfo != null)
        {
            _propertyInfoCache[type] = actionPropertyInfo;
            return actionPropertyInfo;
        }

        // Check States
        foreach (var i in type.GetInterfaces())
        {
            if(typeof(IAction).IsAssignableFrom(i))
            {
                var name = i.GetGenericArguments()[0]
                    .GetProperties()
                    .FirstOrDefault(x => x.GetCustomAttribute<T>(true) != null)?
                    .Name;

                var propertyInfo = type.GetProperties().FirstOrDefault(x => x.Name == name);
                _propertyInfoCache[type] = propertyInfo;
                break;
            }
        }

        return _propertyInfoCache[type];
    }

    public void Set<T>(Type type, params T[] attribute) where T : Attribute
    {
        var key = GetKey<T>(type);
        _stateCache[key] = attribute;
    }

    private static string GetKey<T>(MemberInfo type) => $"{type.Name}-{typeof(T).FullName}";
}
