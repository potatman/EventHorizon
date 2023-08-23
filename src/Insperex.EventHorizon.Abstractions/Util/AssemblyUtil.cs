using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Microsoft.Extensions.DependencyModel;

namespace Insperex.EventHorizon.Abstractions.Util;

public static class AssemblyUtil
{
    private static readonly Assembly Assembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();

    public static readonly ImmutableDictionary<string, Type> TypeDictionary = DependencyContext.Default?.CompileLibraries
        .SelectMany(x =>
        {
            try
            {
                return Assembly.Load(x.Name)?.GetTypes();
            }
            catch (Exception)
            {
                return Array.Empty<Type>();
            }
        })
        .Where(x => x != null)
        .ToLookup(x => x.Name)
        .ToImmutableDictionary(x => x.Key, x => x.Last());

    public static readonly string AssemblyName = Assembly.GetName().Name;
    public static readonly Version AssemblyVersion = Assembly.GetName().Version;
    public static readonly string AssemblyNameAndVersion = $"{AssemblyName}-{AssemblyVersion}";

    public static readonly ImmutableDictionary<string, PropertyInfo[]> PropertyDict = TypeDictionary
        .Where(x => typeof(IState).IsAssignableFrom(x.Value) || typeof(IAction).IsAssignableFrom(x.Value))
        .ToImmutableDictionary(x => x.Key, x => x.Value.GetProperties());

    public static readonly ImmutableDictionary<string, Type> StateDict = TypeDictionary
        .Where(x => typeof(IState).IsAssignableFrom(x.Value))
        .ToImmutableDictionary(x => x.Key, x => x.Value);

    public static readonly ImmutableDictionary<string, Type> ActionDict = TypeDictionary
        .Where(x => typeof(IAction).IsAssignableFrom(x.Value))
        .ToImmutableDictionary(x => x.Key, x => x.Value);

    public static readonly ImmutableDictionary<string, PropertyInfo[]> PropertyDictOfStates = PropertyDict
        .ToImmutableDictionary(x => x.Key, x => x.Value
            .Where(p => typeof(IState).IsAssignableFrom(p.PropertyType)).ToArray());

    public static readonly ImmutableDictionary<string, Type[]> SubStateDict = PropertyDictOfStates
        .ToImmutableDictionary(x => x.Key, x => x.Value.Select(s => s.PropertyType).ToArray());

}
