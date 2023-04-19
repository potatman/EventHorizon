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
    
    private static readonly Assembly[] AllAssemblies = DependencyContext.Default?.CompileLibraries
        .Select(x =>
        {
            try
            {
                return Assembly.Load(x.Name);
            }
            catch (Exception)
            {
                return null;
            }
        })
        .Where(x => x != null)
        .ToArray();

    public static readonly string AssemblyName = Assembly.GetName().Name;
    public static readonly Version AssemblyVersion = Assembly.GetName().Version;
    public static readonly string AssemblyNameAndVersion = $"{AssemblyName}-{AssemblyVersion}";

    public static readonly ImmutableDictionary<Type, string> TypeNameToAssemblyName = AllAssemblies
        .SelectMany(x => x.GetTypes())
        .ToLookup(x => x)
        .ToImmutableDictionary(x => x.Key, x => x.Last().Assembly.GetName().Name);

    public static readonly ImmutableDictionary<string, Type> TypeDictionary = AllAssemblies
        .SelectMany(x => x.GetTypes())
        .ToLookup(x => x.Name)
        .ToImmutableDictionary(x => x.Key, x => x.Last());
    
    public static readonly ImmutableDictionary<string, Type> StateDict = TypeDictionary
        .Where(x => x.Value.GetInterface(nameof(IState)) != null)
        .ToImmutableDictionary(x => x.Key, x => x.Value);
    
    public static readonly ImmutableDictionary<string, PropertyInfo[]> StateToSubStatesPropertyDict = StateDict
        .ToImmutableDictionary(x => x.Key, x => x.Value.GetProperties()
            .Where(p => p.PropertyType.GetInterface(nameof(IState)) != null).ToArray());
    
    public static readonly ImmutableDictionary<string, Type[]> SubStateDict = StateToSubStatesPropertyDict
        .ToImmutableDictionary(x => x.Key, x => x.Value.Select(s => s.PropertyType).ToArray());

    private static readonly string[] ActionTypes = { nameof(IEvent), nameof(ICommand), nameof(IRequest), nameof(IResponse) };
    public static readonly ImmutableDictionary<string, Type> ActionDict = TypeDictionary
        .Where(x => x.Value.GetInterfaces().Any(i => ActionTypes.Contains(i.Name)) )
        .ToImmutableDictionary(x => x.Key, x => x.Value);

}