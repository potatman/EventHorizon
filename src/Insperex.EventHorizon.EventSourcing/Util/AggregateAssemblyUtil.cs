using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Handlers;
using Insperex.EventHorizon.Abstractions.Util;

namespace Insperex.EventHorizon.EventSourcing.Util;

public static class AggregateAssemblyUtil
{
    public static readonly ImmutableDictionary<string, Dictionary<string, MethodInfo>> StateToCommandHandlersDict = GetStateActionHandlerDict(typeof(IHandleCommand<>), "Handle");
    public static readonly ImmutableDictionary<string, Dictionary<string, MethodInfo>> StateToRequestHandlersDict = GetStateActionHandlerDict(typeof(IHandleRequest<,>), "Handle");
    public static readonly ImmutableDictionary<string, Dictionary<string, MethodInfo>> StateToEventHandlersDict = GetStateActionHandlerDict(typeof(IApplyEvent<>), "Apply");

    public static readonly IDictionary<string, Type[]> StateToCommandsLookup = GetStatesToActionLookup(typeof(ICommand<>));
    public static readonly IDictionary<string, Type[]> StateToRequestsLookup = GetStatesToActionLookup(typeof(IRequest<,>));
    public static readonly IDictionary<string, Type[]> StateToEventsLookup = GetStatesToActionLookup(typeof(IEvent<>));

    private static ImmutableDictionary<string, Dictionary<string, MethodInfo>> GetStateActionHandlerDict(MemberInfo type, string methodName) => AssemblyUtil.StateDict
        .ToImmutableDictionary(x => x.Key, x => x.Value.GetInterfaces()
            .Where(i => i.Name == type.Name).ToDictionary(d => d.GetGenericArguments()[0].Name, d => d.GetMethod(methodName)));

    private static IDictionary<string, Type[]> GetStatesToActionLookup(Type type) => AssemblyUtil.StateDict
        .ToDictionary(x => x.Key, x =>
        {
            return AssemblyUtil.ActionDict.Values
                .Where(a => a.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == type && i.GetGenericArguments()[0].Name == x.Key)).ToArray();
        });
}
