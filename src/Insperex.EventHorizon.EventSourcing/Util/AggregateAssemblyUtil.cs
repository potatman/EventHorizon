using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Handlers;
using Insperex.EventHorizon.Abstractions.Util;

namespace Insperex.EventHorizon.EventSourcing.Util;

public static class AggregateAssemblyUtil
{
    public static readonly ImmutableDictionary<string, Dictionary<string, MethodInfo>> StateToCommandHandlersDict = GetStateActionHandlerDict(typeof(IHandleCommand<>), "Handle");
    public static readonly ImmutableDictionary<string, Dictionary<string, MethodInfo>> StateToRequestHandlersDict = GetStateActionHandlerDict(typeof(IHandleRequest<,>), "Handle");
    public static readonly ImmutableDictionary<string, Dictionary<string, MethodInfo>> StateToEventHandlersDict = GetStateActionHandlerDict(typeof(IApplyEvent<>), "Apply");

    public static readonly ILookup<string, Type> StateToCommandsLookup = GetStatesToActionLookup(typeof(ICommand<>));
    public static readonly ILookup<string, Type> StateToRequestsLookup = GetStatesToActionLookup(typeof(IRequest<,>));
    public static readonly ILookup<string, Type> StateToEventsLookup = GetStatesToActionLookup(typeof(IEvent<>));

    private static ImmutableDictionary<string, Type[]> GetStateHandlersDict(MemberInfo type) => AssemblyUtil.StateDict
        .ToImmutableDictionary(x => x.Key, x => x.Value.GetInterfaces().Where(i => i.Name == type.Name).ToArray());

    private static ImmutableDictionary<string, Dictionary<string, MethodInfo>> GetStateActionHandlerDict(MemberInfo type, string methodName) => AssemblyUtil.StateDict
        .ToImmutableDictionary(x => x.Key, x => x.Value.GetInterfaces()
            .Where(i => i.Name == type.Name).ToDictionary(d => d.GetGenericArguments()[0].Name, d => d.GetMethod(methodName)));

    private static ILookup<string, Type> GetStatesToActionLookup(Type type) => AssemblyUtil.TypeDictionary.Values
            .Where(x => x.GetInterface(type.Name) != null)
            .ToLookup(x => x.GetInterface(type.Name)?.GetGenericArguments()[0].Name);
}
