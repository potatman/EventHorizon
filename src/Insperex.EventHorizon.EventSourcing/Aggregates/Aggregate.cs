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

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class Aggregate<T>
    where T : class, IState
{
    private static readonly Type Type = typeof(T);
    private static readonly StateDetail StateDetail = ReflectionFactory.GetStateDetail(Type);
    private Dictionary<Type, object> AllStates { get; set; }

    internal readonly List<Event> Events = new();
    internal readonly List<Response> Responses = new();
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public string Error { get; private set; }
    public bool IsDirty { get; private set; }
    public string Id { get; set; }
    public long SequenceId { get; set; }
    public T Payload { get; set; }
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
        Payload = model.Payload;
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
        var context = new AggregateContext(Exists());
        StateDetail.TriggerHandler(AllStates, context, command);
        foreach(var item in context.Events)
            Apply(new Event(Id, SequenceId, item));
    }

    public void Handle(Request request)
    {
        var context = new AggregateContext(Exists());
        var result = StateDetail.TriggerHandler(AllStates, context, request);
        Responses.Add(new Response(request.Id, request.ResponseTopic, Id, result, Error, (int)StatusCode));
        foreach(var item in context.Events)
            Apply(new Event(Id, SequenceId, item));
    }

    public void Handle(Event @event)
    {
        var context = new AggregateContext(Exists());
        StateDetail.TriggerHandler(AllStates, context, @event);
        foreach(var item in context.Events)
            Apply(new Event(Id, SequenceId, item));
    }

    public void Apply(IEvent<T> @event)
    {
        Apply(new Event(Id, ++SequenceId, @event));
    }

    public void Apply(Event @event, bool isFirstTime = true)
    {
        // Try Self
        StateDetail.TriggerApply(AllStates, @event);

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
        Payload ??= Activator.CreateInstance<T>();
        Payload.Id = Id;
        var properties = StateDetail.PropertiesWithStates;
        AllStates = properties
            .ToDictionary(x => x.PropertyType, x =>
            {
                var value = x.GetValue(Payload);
                if (value != null) return value;

                var state = Activator.CreateInstance(x.PropertyType);
                ((dynamic)state)!.Id = Id;
                x.SetValue(Payload, state);
                return state;
            });
        AllStates[Type] = Payload;
    }

    public bool Exists() => SequenceId > 0;
}
