using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming.Extensions;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class Aggregate<T>
    where T : class, IState
{
    internal readonly List<Event> Events = new();
    internal readonly List<Response> Responses = new();
    private readonly Type _type = typeof(T);
    private readonly StateDetail _stateDetail = new StateDetail(typeof(T));
    private Dictionary<Type, object> AllStates { get; set; }
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
        var payload = command.GetPayload(_stateDetail.CommandDict);
        foreach (var state in AllStates)
        {
            var context = new AggregateContext(Exists());
            var method = ReflectionFactory.GetStateDetail(state.Key).CommandHandlersDict[state.Key.Name].GetValueOrDefault(command.Type);
            method?.Invoke(state.Value, parameters: new [] { payload, context } );
            foreach(var item in context.Events)
                Apply(new Event(Id, SequenceId, item));
        }
    }

    public void Handle(Request request)
    {
        // Try Self
        var payload = request.GetPayload(_stateDetail.RequestDict);
        foreach (var state in AllStates)
        {
            var context = new AggregateContext(Exists());
            var method = ReflectionFactory.GetStateDetail(state.Key).RequestHandlersDict[state.Key.Name].GetValueOrDefault(request.Type);
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
        var payload = @event.GetPayload(_stateDetail.EventDict);
        foreach (var state in AllStates)
        {
            var method = ReflectionFactory.GetStateDetail(state.Key).EventAppliersDict[state.Key.Name].GetValueOrDefault(@event.Type);
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
        var properties = ReflectionFactory.GetStateDetail(_type).PropertiesWithStates;
        AllStates = properties
            .ToDictionary(x => x.PropertyType, x =>
            {
                var state = Activator.CreateInstance(x.PropertyType);
                ((dynamic)state)!.Id = Id;
                x.SetValue(State, state);
                return state;
            });
        AllStates[_type] = State;
    }

    public bool Exists() => SequenceId > 0;

    public Snapshot<T> GetSnapshot() => new(Id, SequenceId, State, CreatedDate, UpdatedDate);
    public View<T> GetView() => new(Id, SequenceId, State, CreatedDate, UpdatedDate);
}
