﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Text.Json;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Models;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventSourcing.Util;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStreaming.Extensions;
using OpenTelemetry.Trace;

namespace EventHorizon.EventSourcing.Aggregates;

public class Aggregate<T>
    where T : class, IState
{
    internal readonly List<Event> Events = new();
    internal readonly List<Response> Responses = new();
    private readonly Type _type = typeof(T);
    private Dictionary<string, object> AllStates { get; set; }
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public string Error { get; private set; }
    public bool IsDirty { get; private set; }
    public string Id { get; set; }
    public long SequenceId { get; set; }
    public T State { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }

    public Aggregate(string streamId)
    {
        Id = streamId;
        CreatedDate = UpdatedDate = DateTime.UtcNow;
        Setup();
    }

    public Aggregate(IStateParent<T> model)
    {
        Id = model.Id;
        SequenceId = model.SequenceId;
        State = model.State;
        CreatedDate = model.CreatedDate;
        UpdatedDate = model.UpdatedDate;
        Setup();
    }

    public Aggregate(MessageContext<Event>[] events)
    {
        // Create
        Setup();
        Id = events.Select(x => x.Data.StreamId).FirstOrDefault();
        CreatedDate = DateTime.UtcNow;

        // Defensive
        if (!events.Any()) return;

        // Apply Events
        foreach (var @event in events)
            Apply(@event.Data, false);
    }

    public void Handle(Command command)
    {
        // Try Self
        var payload = command.GetPayload();
        foreach (var state in AllStates)
        {
            var context = new AggregateContext(Exists());
            var method = AggregateAssemblyUtil.StateToCommandHandlersDict.GetValueOrDefault(state.Key)?.GetValueOrDefault(command.Type);
            method?.Invoke(state.Value, parameters: new [] { payload, context } );
            foreach(var item in context.Events)
                Apply(new Event(Id, SequenceId, item));
        }
    }

    public void Handle(Request request)
    {
        // Try Self
        var payload = request.GetPayload();
        foreach (var state in AllStates)
        {
            var context = new AggregateContext(Exists());
            var method = AggregateAssemblyUtil.StateToRequestHandlersDict.GetValueOrDefault(state.Key)?.GetValueOrDefault(request.Type);
            var result = method?.Invoke(state.Value, parameters: new [] { payload, context } );
            Responses.Add(new Response(request.Id, request.SenderId, Id, result, Error, (int)StatusCode));
            foreach(var item in context.Events)
                Apply(new Event(Id, SequenceId, item));
        }
    }

    public void Apply(IEvent<T> @event)
    {
        Apply(new Event(Id, ++SequenceId, @event));
    }

    public void Apply(Event @event, bool isFirstTime = true)
    {
        // Try Self
        var payload = @event.GetPayload();
        foreach (var state in AllStates)
        {
            var method = AggregateAssemblyUtil.StateToEventHandlersDict.GetValueOrDefault(state.Key)?.GetValueOrDefault(@event.Type);
            method?.Invoke(state.Value, parameters: new [] { payload } );
        }

        // Track Events only if first time applying
        if (isFirstTime)
        {
            @event.SequenceId = ++SequenceId;
            Events.Add(@event);
        }
        else
        {
            SequenceId = @event.SequenceId;
        }

        IsDirty = true;
        UpdatedDate = DateTime.UtcNow;
    }

    public void SetStatus(HttpStatusCode statusCode, string error = null)
    {
        Error = error;
        StatusCode = statusCode;
        foreach (var response in Responses)
        {
            response.StatusCode = (int)statusCode;
            response.Error = error;
        }
    }

    private void Setup()
    {
        // Initialize Data
        State ??= Activator.CreateInstance<T>();
        State.Id = Id;
        var properties = AssemblyUtil.PropertyDictOfStates[_type.Name];
        AllStates = properties
            .ToDictionary(x => x.Name, x =>
            {
                var state = Activator.CreateInstance(x.PropertyType);
                ((dynamic)state)!.Id = Id;
                x.SetValue(State, state);
                return state;
            });
        AllStates[_type.Name] = State;
    }

    public bool Exists() => SequenceId > 0;

    public Snapshot<T> GetSnapshot() => new(Id, SequenceId, State, CreatedDate, UpdatedDate);
    public View<T> GetView() => new(Id, SequenceId, State, CreatedDate, UpdatedDate);
}
