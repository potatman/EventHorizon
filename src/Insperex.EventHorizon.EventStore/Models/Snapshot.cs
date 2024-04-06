using System;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;
using Insperex.EventHorizon.EventStore.Interfaces;

namespace Insperex.EventHorizon.EventStore.Models;

public class Snapshot<T> : IStateParent<T> where T : class
{
    public Snapshot()
    {
    }

    public Snapshot(string streamId, T state)
    {
        // Set Data
        Id = streamId;
        SequenceId = 0;
        Payload = state;

        // Set Dates
        CreatedDate = UpdatedDate = DateTime.UtcNow;
    }

    public Snapshot(string id, long sequenceId, T state, DateTime createdDate, DateTime updatedDate)
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
