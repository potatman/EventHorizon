using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.Abstractions.Util;

/// <summary>
/// Note: this class allows tests to change bucketId
/// </summary>
public class AttributeUtil
{
    private readonly ConcurrentDictionary<string, object[]> _stateCache = new();
    
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
    
    public void Set<T>(Type type, params T[] attribute) where T : Attribute
    {
        var key = GetKey<T>(type);
        _stateCache[key] = attribute;
    }

    private static string GetKey<T>(MemberInfo type) => $"{type.Name}-{typeof(T).Name}";
}