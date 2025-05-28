using System;
using EventHorizon.EventStore.Interfaces;

namespace EventHorizon.EventStore.Models;

public class Snapshot<T> : IStateParent<T>
{
    public Snapshot()
    {
    }

    public Snapshot(string streamId, T state)
    {
        // Set Data
        Id = streamId;
        SequenceId = 0;
        State = state;

        // Set Dates
        CreatedDate = UpdatedDate = DateTime.UtcNow;
    }

    public Snapshot(string id, long sequenceId, T state, DateTime createdDate, DateTime updatedDate)
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
