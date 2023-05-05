using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Exceptions;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces;

namespace Insperex.EventHorizon.EventSourcing.Util;

public class ValidationUtil
{
    private readonly AttributeUtil _attributeUtil;

    public ValidationUtil(AttributeUtil attributeUtil)
    {
        _attributeUtil = attributeUtil;
    }

    public void Validate<T, TS>()
        where T : class, IStateParent<TS>
        where TS : class, IState
    {
        if(typeof(T) == typeof(Snapshot<TS>))
            ValidateSnapshot<TS>();

        if(typeof(T) == typeof(View<TS>))
            ValidateView<TS>();
    }

    public void ValidateSnapshot<T>()
        where T : IState
    {
        var type = typeof(T);
        var types = new[] { type };
        var commandErrors = ValidateHandlers<T, Command>(types);
        var requestErrors = ValidateHandlers<T, Request>(types);
        var eventErrors = ValidateHandlers<T, Event>(types);

        var errors = commandErrors.Concat(requestErrors).Concat(eventErrors).ToArray();
        if (!errors.Any()) return;

        throw new MissingHandlersException(type, AssemblyUtil.SubStateDict[type.Name], types, errors);
    }

    public void ValidateView<T>()
        where T : IState
    {
        var type = typeof(T);
        var eventAttrs = _attributeUtil.GetAll<StreamAttribute>(typeof(T));
        var types = eventAttrs.Select(x => x.SubType).ToArray();

        var errors = ValidateHandlers<T, Event>(types);
        if (!errors.Any()) return;

        throw new MissingHandlersException(type, AssemblyUtil.SubStateDict[type.Name], types, errors);
    }

    private static string[] ValidateHandlers<T, TM>(params Type[] stateTypes)
        where T : IState
        where TM : ITopicMessage
    {
        // Handlers
        var type = typeof(T);
        var allStates = AssemblyUtil.SubStateDict[type.Name].Append(type).ToArray();

        ImmutableDictionary<string, Dictionary<string, MethodInfo>> stateHandlerLookup;
        ILookup<string, Type> stateActionLookup;
        Func<Type, string> getErrorMessage;

        // Register Handlers
        if (typeof(TM) == typeof(Command))
        {
            stateHandlerLookup = AggregateAssemblyUtil.StateToCommandHandlersDict;
            stateActionLookup = AggregateAssemblyUtil.StateToCommandsLookup;
            getErrorMessage = type => $"IHandleCommand<{type.Name}>";
        }
        else if (typeof(TM) == typeof(Request))
        {
            stateHandlerLookup = AggregateAssemblyUtil.StateToRequestHandlersDict;
            stateActionLookup = AggregateAssemblyUtil.StateToRequestsLookup;
            getErrorMessage = type => $"IHandleRequest<{type.Name},{type.GetInterfaces().First().GetGenericArguments()[0].Name}>";
        }
        else if (typeof(TM) == typeof(Event))
        {
            stateHandlerLookup = AggregateAssemblyUtil.StateToEventHandlersDict;
            stateActionLookup = AggregateAssemblyUtil.StateToEventsLookup;
            getErrorMessage = type => $"IApplyEvent<{type.Name}>";
        }
        else
        {
            throw new NotImplementedException();
        }

        // Verify Actions are supported
        var supportedActions =  allStates
            // Get Handlers
            .Select(state => stateHandlerLookup[state.Name])
            .SelectMany(x => x)
            // Get Handler First Parameter
            .Select(x => x.Value?.GetParameters()[0].ParameterType).Distinct().ToArray();

        var allActions = stateTypes.SelectMany(x => stateActionLookup[x.Name])
            // Ignore those with IUpgradeTo
            .Where(x => x.GetInterfaces().All(i => i.Name != typeof(IUpgradeTo<>).Name))
            .Distinct()
            .ToArray();

        var missing = allActions.Where(x => !supportedActions.Contains(x)).ToArray();

        // Return Result
        return missing.Any() ? missing.Select(getErrorMessage).ToArray() : Array.Empty<string>();
    }
}
