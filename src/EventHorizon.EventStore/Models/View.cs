using System;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Serialization.Compression;
using EventHorizon.EventStore.Interfaces;

namespace EventHorizon.EventStore.Models;

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
        Payload = state;

        // Set Dates
        CreatedDate = createdDate == default ? DateTime.UtcNow : createdDate;
        UpdatedDate = updatedDate == default ? CreatedDate : updatedDate;
    }

    public string Id { get; set; }
    public long SequenceId { get; set; }
    public T Payload { get; set; }
    public Compression? Compression { get; set; }
    public byte[] Data { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}
