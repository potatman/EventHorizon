using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces;

namespace Insperex.EventHorizon.EventStore.Models;

public class View<T> : IStateParent<T>
    where T : class, IState
{
    public View()
    {
    }

    public View(string id, long sequenceId, T state, DateTime createdDate, DateTime updatedDate)
    {
        // Set Data
        Id = id;
        SequenceId = sequenceId;
        State = state;

        // Set Dates
        CreatedDate = createdDate == default ? DateTime.UtcNow : createdDate;
        UpdatedDate = updatedDate == default ? CreatedDate : updatedDate;
    }

    public string Id { get; set; }
    public long SequenceId { get; set; }
    public T State { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}