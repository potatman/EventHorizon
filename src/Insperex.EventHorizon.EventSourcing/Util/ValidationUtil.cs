using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Exceptions;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Handlers;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces;
using Microsoft.AspNetCore.Components;

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

    public void ValidateSnapshot<TState>()
        where TState : IState
    {
        var type = typeof(TState);
        var types = new[] { type };
        var stateDetail = ReflectionFactory.GetStateDetail(type);

        var commandErrors = stateDetail.Validate<ICommand>(typeof(IHandleCommand<>),"Handle");
        var requestErrors = stateDetail.Validate<IRequest>(typeof(IHandleRequest<,>),"Handle");
        var eventErrors = stateDetail.Validate<IEvent>(typeof(IApplyEvent<>),"Apply");

        var errors = commandErrors.Concat(requestErrors).Concat(eventErrors).ToArray();
        if (!errors.Any()) return;

        throw new MissingHandlersException(type, stateDetail.SubStates, types, errors);
    }

    public void ValidateView<TState>()
        where TState : IState
    {
        var type = typeof(TState);
        var stateDetail = ReflectionFactory.GetStateDetail(type);
        var eventAttrs = _attributeUtil.GetAll<StreamAttribute>(typeof(TState));
        var types = eventAttrs.Select(x => x.SubType).ToArray();

        var eventErrors = stateDetail.Validate<IEvent>(typeof(IApplyEvent<>),"Apply");
        if (!eventErrors.Any()) return;

        throw new MissingHandlersException(type, stateDetail.SubStates, types, eventErrors);
    }
}
