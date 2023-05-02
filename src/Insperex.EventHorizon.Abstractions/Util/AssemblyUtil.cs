using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyModel;

namespace Insperex.EventHorizon.Abstractions.Util;

public static class AssemblyUtil
{
    private static readonly Assembly Assembly = Assembly.GetCallingAssembly() ?? Assembly.GetEntryAssembly();

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
        .Where(x => x.Value.GetInterface(nameof(IState)) != null || x.Value.GetInterface(nameof(IAction)) != null)
        .ToImmutableDictionary(x => x.Key, x => x.Value.GetProperties());

    public static readonly ImmutableDictionary<string, Type> StateDict = TypeDictionary
        .Where(x => x.Value.GetInterface(nameof(IState)) != null)
        .ToImmutableDictionary(x => x.Key, x => x.Value);

    public static readonly ImmutableDictionary<string, PropertyInfo[]> PropertyDictOfStates = PropertyDict
        .ToImmutableDictionary(x => x.Key, x => x.Value
            .Where(p => p.PropertyType.GetInterface(nameof(IState)) != null).ToArray());

    public static readonly ImmutableDictionary<string, Type[]> SubStateDict = PropertyDictOfStates
        .ToImmutableDictionary(x => x.Key, x => x.Value.Select(s => s.PropertyType).ToArray());

    public static readonly ImmutableDictionary<string, Type> ActionDict = TypeDictionary
        .Where(x => x.Value.GetInterface(nameof(IAction)) != null)
        .ToImmutableDictionary(x => x.Key, x => x.Value);

}
