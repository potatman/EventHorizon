using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventSourcing.Util;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming.Extensions;
using OpenTelemetry.Trace;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class Aggregate<T>
    where T : class, IState
{
    internal readonly List<Event> Events = new();
    internal readonly List<Response> Responses = new();
    private readonly Type _type = typeof(T);
    private Dictionary<string, object> AllStates { get; set; }
    public AggregateStatus Status { get; private set; }
    public bool IsDirty { get; private set; }
    public string Error { get; private set; }
    public string Id { get; set; }
    public long SequenceId { get; set; }
    public T State { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }

    public Aggregate(string streamId)
    {
        Id = streamId;
        SequenceId = 1;
        CreatedDate = UpdatedDate = DateTime.UtcNow;
        Setup();
    }

    public Aggregate(IStateParent<T> model)
    {
        Id = model.Id;
        SequenceId = model.SequenceId + 1;
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
            var list = new List<IEvent>();
            var method = AggregateAssemblyUtil.StateToCommandHandlersDict.GetValueOrDefault(state.Key)?.GetValueOrDefault(command.Type);
            method?.Invoke(state.Value, parameters: new [] { payload, state.Value, list } );
            foreach(var item in list)
                Apply((dynamic)new Event(Id, SequenceId, item));
        }
    }
    
    public void Handle(Request request)
    {
        // Try Self
        var payload = request.GetPayload();
        foreach (var state in AllStates)
        {
            var list = new List<IEvent>();
            var method = AggregateAssemblyUtil.StateToRequestHandlersDict.GetValueOrDefault(state.Key)?.GetValueOrDefault(request.Type);
            var result = method?.Invoke(state.Value, parameters: new [] { payload, state.Value, list } );
            Responses.Add(new Response(Id, request.Id, request.SenderId, result) { Status = Status, Error = Error});
            foreach(var item in list)
                Apply((dynamic)new Event(Id, SequenceId, item));
        }
    }
    
    public void Apply(IEvent<T> @event)
    {
        Apply(new Event(Id, SequenceId, @event));
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
            @event.SequenceId = SequenceId;
            Events.Add(@event);
        }
        else
        {
            SequenceId = @event.SequenceId;
        }

        IsDirty = true;
        UpdatedDate = DateTime.UtcNow;
    }

    public void SetStatus(AggregateStatus status, string error = null)
    {
        Status = status;
        Error = error;
        foreach (var response in Responses)
        {
            response.Status = status;
            response.Error = error;
        }
    }

    private void Setup()
    {
        // Initialize Data
        State ??= Activator.CreateInstance<T>();
        State.Id = Id;
        var properties = AssemblyUtil.StateToSubStatesPropertyDict[_type.Name];
        AllStates = properties
            .ToDictionary(x => x.Name, x =>
            {
                var state = Activator.CreateInstance(x.PropertyType);
                ((dynamic)state).Id = Id;
                x.SetValue(State, state);
                return state;
            });
        AllStates[_type.Name] = State;
    }

    public Snapshot<T> GetSnapshot() => new(Id, SequenceId, State, CreatedDate, UpdatedDate);
    public View<T> GetView() => new(Id, SequenceId, State, CreatedDate, UpdatedDate);
}